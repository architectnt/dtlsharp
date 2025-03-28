/*
    This is a part of DigitalOut and is licenced under MIT.
*/

using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System.Diagnostics;
using dtl.Internal;
using dtl.Internal.Native;
using System.Text;
using CatBox.NET.Requests;
using System.Runtime.InteropServices;

namespace dtl.modules {
    public enum CodecType {
        h264,
        hevc,
    }

    public enum Resolution
    {
        SD,
        HD,
        FHD
    }

    [CommandContextType(InteractionContextType.PrivateChannel, InteractionContextType.BotDm, InteractionContextType.Guild)]
    [IntegrationType(ApplicationIntegrationType.UserInstall, ApplicationIntegrationType.GuildInstall)]
    public class Base : InteractionModuleBase<ShardedInteractionContext>
    {

        [SlashCommand("dtlrend", "convert chiptune to audio")]
        public async Task Fur2mp3(IAttachment attachment = null, string url = null, FileFormat format = FileFormat.mp3, uint subsong = 0, uint loopsOrDuration = 0, CodecType codecType = CodecType.h264, Resolution res = Resolution.FHD) {
            List<string> 
                furmats = [".ftm", ".dmf", ".fc13", ".fc14", ".fc", ".0cc", ".dnm", ".eft", ".fub", ".fte", ".fur"], 
                midi = [".mid", ".midi"], 
                sid = [".sid"],
                libmodplug = [".mptm", ".xm", ".s3m", ".it", ".mod"],
                libgme = [".ay", ".gbs", ".gym", ".hes", ".kss", ".nsf", ".nsfe", ".sap", ".spc", ".vgm", ".vgz"];

            int i, j, len = 0;

            if (attachment == null && url == null) {
                FileAttachment[] s = [new(new MemoryStream(await File.ReadAllBytesAsync(".core/lgo.png")), "logo.png")];

                List<string> combined = [..furmats, ..midi, ..sid, ..libmodplug, ..libgme];
                string fm = null;
                for(i = 0; i < combined.Count; i++)
                    fm += $"`{combined[i]}` ";

                EmbedBuilder builder = new() {
                    Title = "digitalout_",
                    Description = "Open source Discord bot capable of rendering various chiptune formats\n\n" + 
                    $"## *THIS INSTANCE*\nSupported formats: {fm}\n\n" +
                    $"-# *Hardware accelerated:* `{(GPUDetector.GetGPUType() != GPUType.NONE ? "yes" : "no")}`\n" +
                    $"-# *AAFC Version:* `v{LibAAFC.Version}`",
                    Color = API.RedColor,
                    ThumbnailUrl = $"attachment://{s[0].FileName}"
                };
                await Context.Interaction.RespondWithFilesAsync(s, embed: builder.Build(), allowedMentions: AllowedMentions.None);
                return;
            }

            await DeferAsync();
            Stopwatch sw = Stopwatch.StartNew();

            string curl = url ?? attachment.Url;
            string n = url != null ? Path.GetFileName(API.RemoveQueryParameters(url)) : attachment.Filename;

            if(!await WebClient.IsValidUrl(curl)){
                await FollowupAsync($"LOL");
                return;
            }

            byte[] dt = await WebClient.GetDataAsync(curl);
            byte[] fnbytes = Encoding.Unicode.GetBytes(n);
            ulong hash = 0; // not bothered using sha
            for(i = 0; i < dt.Length; i++){
                hash ^= dt[i] + (ulong)i + fnbytes[i % fnbytes.Length];
            }
            hash += loopsOrDuration + subsong + (uint)format;

            string tmpfoldr = $"{API.tmpdir}/{Random256.Value}";
            Directory.CreateDirectory(tmpfoldr);
            string ext = Path.GetExtension(n).ToLower();
            ComponentResult r = new();
            List<short[]> channels = [];
            List<(byte[] dt, string name, float amp, long timestamp)> outputdt = [];
            float[] mst = null;
            IUserMessage t = null;
            ComponentBuilder cns = new();
            cns.WithButton($"cancel", "r_cancel", style: ButtonStyle.Danger);
            IUser orgusr = Context.User;
            string cff = null;
            bool oscRender = format == FileFormat.mp4,
            processing = false;

            Thread frontend = new(async()=>{ // just so that it DOESN'T THROW AN ERRO
                await Task.Delay(60 * 1000);
                if(processing) await ModifyOriginalResponseAsync(m => m.Content = $"{cff}\n-# This will take a while...");
                while(processing){
                    await Task.Delay(30000);
                    if (processing) await ModifyOriginalResponseAsync(m => m.Content = $"{cff}\n-# {API.FriendlyTimeFormat(sw.Elapsed)} elapsed");
                    else break;
                }
            });
            frontend.Start();

            CancellationTokenSource cf = new();
            async Task CancelButton(SocketMessageComponent btn)
            {
                if (btn.Message.Id != t.Id) return;
                if (btn.Data.CustomId == "r_cancel") {
                    if (btn.User.Id != orgusr.Id) {
                        await btn.RespondAsync($"you cannot cancel {orgusr.Mention}'s render.", ephemeral: true);
                        return;
                    }

                    cf.Cancel();
                    t = await Context.Interaction.ModifyOriginalResponseAsync(m => {
                        m.Content = "canceled";
                        m.Components = null;
                    });
                    processing = false;
                }
            }

            try {
                Task renderTask = Task.Run(async () => {
                    Program.client.ButtonExecuted += CancelButton;
                    processing = true;
                    if(!API.modulecache.ContainsKey(hash)) {
                        if (furmats.Contains(ext)) {
                            cff = oscRender ? "Seperating channels..." : "Rendering..";
                            t = await ModifyOriginalResponseAsync(m => {
                                m.Content = cff;
                                m.Components = cns.Build();
                            });

                            File.WriteAllBytes($"{tmpfoldr}/{n}", dt);
                            ulong s = Random256.Value;
                            string what = $"{tmpfoldr}/{s}.wav";
                            r = await ProcessHandler.Furnace($"{tmpfoldr}/{n}", what, oscRender, loopsOrDuration, subsong, cf.Token);
                            if (!oscRender)
                                r = await ProcessHandler.ConvertMediaStdOut(what, "wav", ct: cf.Token); // pass to std out
                        } else if (libmodplug.Contains(ext)) {
                            cff = "Rendering..";
                            t = await ModifyOriginalResponseAsync(m => {
                                m.Content = cff;
                                m.Components = cns.Build();
                            });

                            File.WriteAllBytes($"{tmpfoldr}/{n}", dt);
                            ulong s = Random256.Value;
                            string what = $"{tmpfoldr}/{s}";
                            r = await ProcessHandler.MPTSplit($"{tmpfoldr}/{n}", what, loopsOrDuration, !oscRender, cf.Token);
                            if (!oscRender)
                                r = await ProcessHandler.ConvertMediaStdOut($"{tmpfoldr}/{s}_master.wav", "wav", ct: cf.Token);
                        }
                        else if (libgme.Contains(ext))
                        {
                            cff = "Rendering..";
                            t = await ModifyOriginalResponseAsync(m => {
                                m.Content = cff;
                                m.Components = cns.Build();
                            });

                            File.WriteAllBytes($"{tmpfoldr}/{n}", dt);
                            ulong s = Random256.Value;
                            r = await ProcessHandler.VGMSplit(n, tmpfoldr, subsong, !oscRender, cf.Token);
                            if (!oscRender)
                            {
                                r = await ProcessHandler.ConvertMediaStdOut($"{tmpfoldr}/{Path.GetFileNameWithoutExtension(n)}.wav", "wav", ct: cf.Token); // pass to std out
                            }
                            else
                            {
                                File.Delete($"{tmpfoldr}/{Path.GetFileNameWithoutExtension(n)}.wav"); // because vgmsplit creates a master channel as well
                            }
                        }
                        else if (midi.Contains(ext))
                        {
                            cff = "Rendering..";
                            t = await ModifyOriginalResponseAsync(m => {
                                m.Content = cff;
                                m.Components = cns.Build();
                            });

                            if (oscRender)
                            {
                                File.WriteAllBytes($"{tmpfoldr}/{n}", dt);
                                r = await ProcessHandler.MidiToAudio($"{tmpfoldr}/{n}", $"{tmpfoldr}/huhhhhhhh.wav", cf.Token);
                            }
                            else r = await ProcessHandler.MidiToAudioInternal(dt, "wav", cf.Token);
                            cf.Token.ThrowIfCancellationRequested();
                        }
                        else if (sid.Contains(ext))
                        {
                            cff = oscRender ? "Seperating channels..." : "Rendering..";
                            t = await ModifyOriginalResponseAsync(m => {
                                m.Content = cff;
                                m.Components = cns.Build();
                            });

                            File.WriteAllBytes($"{tmpfoldr}/{n}", dt);
                            ulong s = Random256.Value;
                            if (!oscRender)
                            {
                                r = await ProcessHandler.Sid2Wav(tmpfoldr, n, $"{s}.wav");
                                r = await ProcessHandler.ConvertMediaStdOut($"{tmpfoldr}/{s}.wav", "wav", ct: cf.Token);
                            }
                            else
                            {
                                r = await ProcessHandler.Sid2Wav(tmpfoldr, n, $"{s}_01.wav", $"-u2 -u3", cf.Token);
                                r = await ProcessHandler.Sid2Wav(tmpfoldr, n, $"{s}_02.wav", $"-u1 -u3", cf.Token);
                                r = await ProcessHandler.Sid2Wav(tmpfoldr, n, $"{s}_02.wav", $"-u1 -u2", cf.Token);
                            }
                        }
                        else
                        {
                            r.exitcode = 0xFFFFFFF;
                            r.message = $"not a valid format | Not defined or not implemented ({ext})";
                            return;
                        }
                        if (cf.IsCancellationRequested) return;
                    }


                    if (r.exitcode != 0) // catch failure from first rendering pass
                        return;

                    if (oscRender)
                    {
                        if(!API.modulecache.TryGetValue(hash, out List<(byte[] dt, string name, float amp, long timestamp)> val)) {
                            cff = "post processing audio..";
                            await ModifyOriginalResponseAsync(m => m.Content = cff);

                            FileInfo[] c = [..new DirectoryInfo(tmpfoldr).GetFiles("*.wav").OrderBy(f => f.CreationTimeUtc)];
                            int lchn = 0; // the loudest channel value;
                            for (i = 0; i < c.Length; i++)
                            {
                                r = await ProcessHandler.ConvertMediaStdOut(c[i].FullName, "s16le", args: $"-ac 2 -ar 44100", ct: cf.Token);
                                short[] samples = MemoryMarshal.Cast<byte, short>(r.stdout).ToArray(); // fucking shit
                                if (!samples.All(value => value == 0))
                                {
                                    for (j = 0; j < samples.Length; j++)
                                    {
                                        int mp = Math.Abs((int)samples[j]);
                                        if (mp > lchn) lchn = mp;
                                    }
                                    float ampc = ((float)short.MaxValue / lchn) * 0.85f;
                                    outputdt.Add((WavUtility.Export(samples, 44100, 2), $"{i}.wav", ampc, DateTimeOffset.Now.ToUnixTimeSeconds()));
                                }
                                channels.Add(samples);
                            }
                            for (i = 0; i < channels.Count; i++)
                            {
                                if (len < channels[i].Length)
                                    len += channels[i].Length;
                            }
                            mst = new float[len];
                            for (i = 0; i < channels.Count; i++)
                            {
                                for (j = 0; j < len; j++)
                                    mst[j] += j < channels[i].Length ? (channels[i][j] / (float)short.MaxValue) : 0.0f;
                            }
                            if (outputdt.Count == 0)
                            {
                                r.exitcode = 0xFACC; // fuck.
                                r.message = $"all channels are silent.";
                                return;
                            }
                            AudioClip clip = LibAAFC.Import(LibAAFC.Export(mst, 2, 44100, nm: true), null);
                            short[] outp = clip.ToShortSamples();
                            outputdt.Add((WavUtility.Export(outp, 44100, 2), "master.wav", 0, DateTimeOffset.Now.ToUnixTimeSeconds()));

                            API.modulecache[hash] = outputdt;
                            clip.Dispose();
                        } else{
                            outputdt = val;
                        }

                        List<CorrscopeEntry> entries = [];
                        for (i = 0; i < outputdt.Count; i++)
                        {
                            string p = $"{Directory.GetCurrentDirectory()}/{tmpfoldr}/{outputdt[i].name}";
                            await File.WriteAllBytesAsync(p, outputdt[i].dt);
                            if (i < outputdt.Count - 1)
                            {
                                entries.Add(new()
                                {
                                    path = p,
                                    amplify = outputdt[i].amp,
                                });
                            }
                        }

                        (uint w, uint h) = res switch // WHY ROSLYN IM GONNA "BYTE" YOU!!11
                        {
                            Resolution.FHD => ((uint)1920, (uint)1080),
                            Resolution.HD => ((uint)1280, (uint)720),
                            Resolution.SD => ((uint)640, (uint)480),
                            _ => ((uint)320, (uint)240)
                        };

                        cff = "Rendering oscilloscope video";
                        await File.WriteAllTextAsync($"{tmpfoldr}/osc.yaml", CorrscopeWrapper.CreateCorrscopeOverrides(format, codecType, $"{Directory.GetCurrentDirectory()}/{tmpfoldr}/master.wav", [.. entries], w, h), cf.Token);
                        await ModifyOriginalResponseAsync(m => m.Content = cff);

                        r = await ProcessHandler.RenderCorrscopeVideo($"{tmpfoldr}/osc.yaml", $"{tmpfoldr}/oscoutp.{format}", cf.Token);
                        if(r.exitcode != 0){
                            r.exitcode = 0x01010101;
                            r.message = $"osc rendering failed. ({r.message})";
                            return;
                        }

                        string codec = ProcessHandler.GetHWAccelCodec(GPUDetector.GetGPUType(), format, codecType);

                        string hw = GPUDetector.GetGPUType() switch
                        {
                            GPUType.NONE => null,
                            GPUType.NV => $"-hwaccel cuda",
                            GPUType.RADEON => $"-hwaccel vulkan",
                            GPUType.ARC => $"-hwaccel qsv",
                            GPUType.APPLESILICON => "-hwaccel videotoolbox",
                            _ => null,
                        };

                        cff = "fragmenting video..";
                        await ModifyOriginalResponseAsync(m => m.Content = cff);
                        r = await ProcessHandler.ConvertMediaStdOut($"{tmpfoldr}/oscoutp.{format}", $"{format}", hw, args: $"-b:v 1512k -b:a 192k -movflags +faststart+frag_keyframe+empty_moov+default_base_moof", ct: cf.Token);
                    }
                    else
                    {
                        if (!API.modulecache.TryGetValue(hash, out List<(byte[] dt, string name, float amp, long timestamp)> val))
                        {
                            outputdt.Add((r.stdout, null, 0, DateTimeOffset.Now.ToUnixTimeSeconds()));
                            API.modulecache[hash] = outputdt;
                        }
                        else
                        {
                            outputdt = val;
                        }
                        r = await ProcessHandler.ConvertMediaInternal(outputdt[0].dt, $"{format}", ct: cf.Token);
                    }
                    if (cf.IsCancellationRequested) return;
                }, cf.Token);

                try {
                    await renderTask;
                } catch (Exception s) {
                    if(s is OperationCanceledException)
                    {
                        Console.WriteLine("task canceled");
                    }
                    else {
                        r.exitcode = 0xFFCC;
                        r.message = $"rendering task failure ({s.Message})";
                    }
                    return;
                }
                goto finalize;
            }
            catch (Exception ex) {
                processing = false;
                Console.Write(ex);

                r.exitcode = 0xFFAA;
                r.message = $"rendering failed. ({ex.Message})";
                goto failure;
            }

            finalize: {
                try
                {
                    string fn = $"{Path.GetFileNameWithoutExtension(n)}.{format}";
                    sw.Stop();
                    processing = false;
                    cff = "finishing..";
                    string externurl = null;
                    if (r.exitcode != 0) goto failure;
                    await ModifyOriginalResponseAsync(m => m.Content = cff);
                    MemoryStream m = new(r.stdout);
                    if (r.stdout.LongLength > 10000000) // WHY IS IT 10 MB DISCORD SHOW YOURSELF
                    {
                        if(API.settings.usecatbox){
                            cff = "Bringing the heavy lifting externally..";
                            await ModifyOriginalResponseAsync(m => m.Content = cff);
                            var resp = await WebClient.GetLiterBoxInstance().UploadImage(new TemporaryStreamUploadRequest(){
                                Expiry = CatBox.NET.Enums.ExpireAfter.ThreeDays,
                                FileName = fn,
                                Stream = m,
                            });
                            externurl = resp;
                        }else{
                            r.exitcode = 0xDDFA14;
                            r.message = $"over 10mb.";
                            goto failure;
                        }
                    }
                    ComponentBuilder cb = new ComponentBuilder().WithButton($"get {ext} file", style: ButtonStyle.Link, url: curl);
                    FileAttachment[] s = externurl != null 
                        ? null 
                        : [new(m, $"{Path.GetFileNameWithoutExtension(n)}.{format}")];


                    await ModifyOriginalResponseAsync(m =>
                    {
                        m.Content = $"{Context.User.Mention}'s render finished!\n-# **time:** *{API.FriendlyTimeFormat(sw.Elapsed)}*\n***{(externurl != null ? $"[{n}]({externurl})":n)}***";
                        if (s != null)
                            m.Attachments = s;
                        m.Components = cb.Build();
                    });
                    Directory.Delete(tmpfoldr, true);
                    Program.client.ButtonExecuted -= CancelButton;
                    return;
                } catch(Exception ex) {
                    r.exitcode = 0xEEFFAA;
                    r.message = $"failed finalizing: {ex.Message}";
                    goto failure;
                }
            }


            failure: {
                processing = false;
                t = await Context.Interaction.ModifyOriginalResponseAsync(m => {
                    m.Content = cff + $"\n-# FAILED";
                    m.Components = null;
                });
                EmbedBuilder e = new()
                {
                    Color = API.RedColor,
                    Title = $"Error processing result",
                    Description = $"```ansi\n\u001b[2;31m{r.exitcode} \u001b[2;30m| \u001b[2;37m{r.message}\u001b[0m```\n"
                };
                await Context.Interaction.FollowupAsync(embed: e.Build());
                return;
            }
        }
    }
}
