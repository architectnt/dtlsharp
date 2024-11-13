/*
    This is a part of fur2mp3 Rewrite and is licenced under MIT.
*/

using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System.Diagnostics;
using fur2mp3.Internal;
using fur2mp3.Internal.Native;

namespace fur2mp3.module {
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


        [SlashCommand("fur2mp3", "convert chiptune to audio")]
        public async Task Fur2mp3(IAttachment attachment = null, string url = null, FileFormat format = FileFormat.mp3, uint subsong = 0, uint loopsOrDuration = 0, CodecType codecType = CodecType.h264, Resolution res = Resolution.FHD) {
            List<string> 
                furmats = [".ftm", ".dmf", ".fc13", ".fc14", ".mod", ".fc", ".0cc", ".dnm", ".eft", ".fub", ".fte", ".fur"], 
                midi = [".mid", ".midi"], 
                sid = [".sid"],
                libmodplug = [".mptm", ".xm", ".s3m", ".it"],
                libgme = [".ay", ".gbs", ".gym", ".hes", ".kss", ".nsf", ".nsfe", ".sap", ".spc", ".vgm", ".vgz"];

            int i, j, len = 0;

            if (attachment == null && url == null) {
                List<string> combined = [..furmats, ..midi, ..sid, ..libmodplug, ..libgme];
                string fm = null;
                for(i = 0; i < combined.Count; i++)
                    fm += $"`{combined[i]}` ";

                EmbedBuilder builder = new()
                {
                    Title = "FUR2MP3 REWRITE",
                    Description = $"Supported formats: {fm}\n",
                    Color = API.RedColor,
                };
                await Context.Interaction.RespondAsync(embed: builder.Build(), allowedMentions: AllowedMentions.None);
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
            ulong hash = 0; // not bothered using sha
            for(i = 0; i < dt.Length; i++){
                hash ^= dt[i];
            }
            hash += loopsOrDuration + subsong;

            string tmpfoldr = $"{API.tmpdir}/instance_{Random256.Value}";
            Directory.CreateDirectory(tmpfoldr);
            string ext = Path.GetExtension(n).ToLower();
            ComponentResult r = new();
            List<short[]> channels = [];
            List<(byte[] dt, string name, float amp)> outputdt = [];
            float[] mst = null;
            IUserMessage t = null;
            ComponentBuilder cns = new();
            cns.WithButton($"cancel", "r_cancel", style: ButtonStyle.Danger);
            IUser orgusr = Context.User;
            Dictionary<ulong, int> externalcanceltimes = [];
            string cff = null;
            bool oscRender = format == FileFormat.mp4 || format == FileFormat.webm,
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

            try {
                CancellationTokenSource cf = new();
                Task renderTask = Task.Run(async () =>
                {
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
                            {
                                r = await ProcessHandler.ConvertMediaStdOut(what, "wav", ct: cf.Token); // pass to std out
                            }
                        } else if (libmodplug.Contains(ext)) {
                            cff = "Rendering..";
                            t = await ModifyOriginalResponseAsync(m => {
                                m.Content = cff;
                                m.Components = cns.Build();
                            });

                            if (oscRender)
                            {
                                File.WriteAllBytes($"{tmpfoldr}/{n}", dt);
                                ulong s = Random256.Value;
                                string what = $"{tmpfoldr}/{s}.wav";
                                r = await ProcessHandler.Furnace($"{tmpfoldr}/{n}", what, oscRender, loopsOrDuration, subsong, cf.Token);
                            }
                            else r = await ProcessHandler.ConvertMediaStdOut(curl, "wav", $"-f libmodplug", ct: cf.Token);
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
                            r = await ProcessHandler.VGMSplit(n, tmpfoldr, !oscRender, cf.Token);
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

                    if (oscRender)
                    {
                        if(!API.modulecache.TryGetValue(hash, out List<(byte[] dt, string name, float amp)> val)) {
                            cff = "Applying additional encoding";
                            await ModifyOriginalResponseAsync(m => m.Content = cff);

                            FileInfo[] c = [..new DirectoryInfo(tmpfoldr).GetFiles("*.wav").OrderBy(f => f.CreationTimeUtc)];
                            int lchn = 0; // the loudest channel value;
                            for (i = 0; i < c.Length; i++)
                            {
                                r = await ProcessHandler.ConvertMediaStdOut(c[i].FullName, "s16le", args: $"-ac 2 -ar 44100", ct: cf.Token);
                                short[] samples = new short[r.stdout.Length / 2];
                                Buffer.BlockCopy(r.stdout, 0, samples, 0, r.stdout.Length);
                                if (!samples.All(value => value == 0))
                                {
                                    for (j = 0; j < samples.Length; j++)
                                    {
                                        int mp = Math.Abs((int)samples[j]);
                                        if (mp > lchn) lchn = mp;
                                    }
                                    float ampc = ((float)short.MaxValue / lchn) * 0.85f;
                                    outputdt.Add((WavUtility.Export(samples, 44100, 2, 16), $"{i}.wav", ampc));
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
                                r.exitcode = 0xFFFACCC;
                                r.message = $"all channels are silent.";
                                return;
                            }

                            short[] outp = LibAAFC.ToShortSamples(LibAAFC.Import(LibAAFC.Export(mst, 2, 44100, nm: true), null));
                            outputdt.Add((WavUtility.Export(outp, 44100, 2, 16), "master.wav", 0));
                            
                            API.modulecache[hash] = outputdt;
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
                        await File.WriteAllTextAsync($"{tmpfoldr}/osc.yaml", CorrscopeWrapper.CreateCorrscopeOverrides(format, $"{Directory.GetCurrentDirectory()}/{tmpfoldr}/master.wav", [.. entries], w, h), cf.Token);
                        await ModifyOriginalResponseAsync(m => m.Content = cff);

                        if((r = await ProcessHandler.RenderCorrscopeVideo($"{tmpfoldr}/osc.yaml", $"{tmpfoldr}/oscoutp.{format}", cf.Token)).exitcode != 0){
                            r.exitcode = 0x01010101;
                            r.message = $"osc rendering failed. ({r.message})";
                            return;
                        }

                        string codec = ProcessHandler.GetHWAccelCodec(GPUDetector.GetGPUType(), format);

                        string hw = GPUDetector.GetGPUType() switch
                        {
                            GPUType.NONE => null,
                            GPUType.NV => $"-hwaccel cuda",
                            GPUType.RADEON => $"-hwaccel vulkan",
                            GPUType.ARC => $"-hwaccel qsv",
                            GPUType.APPLESILICON => "-hwaccel videotoolbox",
                            _ => null,
                        };

                        if (new FileInfo($"{tmpfoldr}/oscoutp.{format}").Length >= 26214400) // efficiently check filesize
                        {
                            int tbitrate = 20 * 8192 / (int)(1.048576f *(len / 44100)) - 192;

                            cff = "Compressing video..";
                            await ModifyOriginalResponseAsync(m => m.Content = cff);
                            r = await ProcessHandler.ConvertMediaStdOut($"{tmpfoldr}/oscoutp.{format}", $"{format}", hw, args: $"-c:v {codec} -b:v {tbitrate}k -maxrate {tbitrate}k -bufsize {tbitrate * 2}k -b:a 192k {(format == FileFormat.mp4 
                                ? "-movflags +faststart+frag_keyframe+empty_moov+default_base_moof" 
                                : null)}", ct: cf.Token);
                        }
                        else
                        {
                            cff = "fragmenting..";
                            await ModifyOriginalResponseAsync(m => m.Content = cff);
                            r = await ProcessHandler.ConvertMediaStdOut($"{tmpfoldr}/oscoutp.{format}", $"{format}", hw, args: $"-c:v {codec} -b:a 192k {(format == FileFormat.mp4 
                                ? "-movflags +faststart+frag_keyframe+empty_moov+default_base_moof" 
                                : null)}", ct: cf.Token);
                        }
                    }
                    else
                    {
                        r = await ProcessHandler.ConvertMediaInternal(r.stdout, $"{format}", ct: cf.Token);
                    }
                    if (cf.IsCancellationRequested) return;
                }, cf.Token);
                
                async Task CancelButton(SocketMessageComponent btn)
                {
                    if (btn.Message.Id != t.Id) return;
                    if(btn.User.Id != orgusr.Id) {
                        if(externalcanceltimes.ContainsKey(btn.User.Id)) {
                            externalcanceltimes[btn.User.Id]++;
                        }
                        else {
                            externalcanceltimes.Add(btn.User.Id, 1); 
                            cff = t.Content;
                        }
                        string cft = cff;
                        for(int i = 0; i < externalcanceltimes.Count; i++){
                            IUser s = Program.client.GetUser(externalcanceltimes.ElementAt(i).Key);
                            int cfgb = externalcanceltimes.ElementAt(i).Value;
                            cft += $"\n-# {(s == null ? "unknown" : s.Mention)} tried to cancel {orgusr.Mention}'s render 🪑";
                            if(cfgb > 1) cft += $" **{cfgb}** times!"; 
                        }

                        t = await Context.Interaction.ModifyOriginalResponseAsync(m => {
                            m.Content = cft;
                        });
                        return;
                    }
                    if (btn.Data.CustomId == "r_cancel")
                    {
                        cf.Cancel();
                        t = await Context.Interaction.ModifyOriginalResponseAsync(m => {
                            m.Content = "canceled";
                            m.Components = null;
                        });
                    }
                }

                try {
                    await renderTask;
                } catch (OperationCanceledException) {
                    Console.WriteLine("task canceled");
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
                    sw.Stop();
                    processing = false;
                    cff = "Finalizing";
                    await ModifyOriginalResponseAsync(m => m.Content = cff);
                    if (r.exitcode != 0) goto failure;
                    if (r.stdout.LongLength > 26214400)
                    {
                        r.exitcode = 0xAFD013F;
                        r.message = "result went over 25mb";
                        goto failure;
                    }
                    FileAttachment[] s = [new(new MemoryStream(r.stdout), $"{Path.GetFileNameWithoutExtension(n)}.{format}")];
                    ComponentBuilder cb = new();
                    cb.WithButton($"get {ext} file", style: ButtonStyle.Link, url: curl);

                    await ModifyOriginalResponseAsync(m =>
                    {
                        m.Content = $"{Context.User.Mention}'s render finished!\n***{n}***\n-# **time taken:** *{API.FriendlyTimeFormat(sw.Elapsed)}*";
                        m.Attachments = s;
                        m.Components = cb.Build();
                    });

                    Directory.Delete(tmpfoldr, true);
                    return;
                } catch(Exception ex) {
                    r.exitcode = 0xEEFFAA;
                    r.message = $"failed finalizing: {ex}";
                    goto failure;
                }
            }


            failure: {
                processing = false;
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
