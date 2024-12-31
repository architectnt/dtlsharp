/*
    This is a part of DigitalOut and is licenced under MIT.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace dtl.Internal {
    /// <summary>
    /// Results for external components
    /// </summary>
    public struct ComponentResult {
        public byte[] stdout;
        public int exitcode;
        public string message;
    }

    public enum FileFormat {
        mp3,
        ogg,
        opus,
        flac,
        mp4,
        webm,
    }

    public static class ProcessHandler
    {

        public static string GetHWAccelCodec(GPUType gpuType, FileFormat format) => (gpuType, format) switch {
            (GPUType.NONE, FileFormat.mp4) => "libx264",
            (GPUType.NV, FileFormat.mp4) => "h264_nvenc",
            (GPUType.RADEON, FileFormat.mp4) => "h264_amf",
            (GPUType.ARC, FileFormat.mp4) => "h264_qsv",
            (GPUType.APPLESILICON, FileFormat.mp4) => "h264_videotoolbox",
            (GPUType.NONE, FileFormat.webm) => "libvpx",
            (GPUType.NV, FileFormat.webm) => "libvpx", // GOD DAMN IT
            (GPUType.RADEON, FileFormat.webm) => "libvpx", // gotcha: may not support vp9
            (GPUType.ARC, FileFormat.webm) => "libvpx", // go back and provide hardware accel if needed THIS IS ABYSMAL
            _ => "libx264"
        };

        public static string GetAudioCodec(FileFormat format) => format switch {
            FileFormat.mp3 => "mp3",
            FileFormat.ogg => "libvorbis",
            FileFormat.opus => "libopus",
            FileFormat.webm => "libopus",
            FileFormat.mp4 => "aac",
            _ => null // if you know you know
        };

        public static string GetFFMPEGFormat(string filename)
        {
            string fmt = Path.GetExtension(filename).ToLower().Replace(".", null).Trim();
            switch (fmt)
            {
                case "opus":
                    fmt = "ogg";
                    break;
                case "mkv":
                    fmt = "matroska";
                    break;
                case "ay":
                case "gbs":
                case "gym":
                case "hes":
                case "kss":
                case "nsf":
                case "nsfe":
                case "sap":
                case "spc":
                case "vgm":
                case "vgz":
                    fmt = "libgme";
                    break;
                case "mptm":
                case "xm":
                case "mod":
                case "s3m":
                case "it":
                    fmt = "libmodplug";
                    break;
                default:
                    break;
            }
            return fmt;
        }


        /// <summary>
        /// furnace out an audio file
        /// </summary>
        /// <returns>a furnaced wav</returns>
        public static async Task<ComponentResult> Furnace(string input, string outp, bool perchan = false, uint loops = 0, uint subsong = 0, CancellationToken ct = default)
        {
            using Process fpc = Process.Start(new ProcessStartInfo()
            {
                FileName = ".core/components/furnace",
                Arguments = $"-loglevel error -nostatus -subsong {subsong} -loops {loops} {(perchan ? "-outmode perchan" : null)} -output \"{outp}\" \"{input}\"",
                UseShellExecute = false,
            });
            await fpc.WaitForExitAsync(ct).ConfigureAwait(false);
            return new()
            {
                exitcode = fpc.ExitCode,
                message = "error message not available"
            };
        }

        public static async Task<ComponentResult> ConvertMedia(string input, string output, string innerargs = null, string args = null) {
            using Process fpc = Process.Start(new ProcessStartInfo()
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel error -y {innerargs} -i \"{input}\" -threads {Environment.ProcessorCount} {args} \"{output}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            await fpc.WaitForExitAsync().ConfigureAwait(false);
            return new()
            {
                exitcode = fpc.ExitCode,
                message = fpc.StandardError.ReadToEnd()
            };
        }

        public static async Task<ComponentResult> ConvertMediaInternal(byte[] input, string format, string innerargs = null, string args = null, CancellationToken ct = default)
        {
            using Process fpc = Process.Start(new ProcessStartInfo()
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel error -y {innerargs} -i - -threads {Environment.ProcessorCount} {args} -f {format} -",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            using Stream stdin = fpc.StandardInput.BaseStream;
            using Stream stdout = fpc.StandardOutput.BaseStream;
            using MemoryStream output = new();

            try
            {
                Task writer = Task.Run(async () =>
                {
                    await stdin.WriteAsync(input);
                    await stdin.FlushAsync();
                    stdin.Close();
                }, ct);

                Task reader = Task.Run(async () => {
                    byte[] buffer = new byte[8192];

                    int l;
                    while ((l = await stdout.ReadAsync(buffer)) > 0)
                    {
                        await output.WriteAsync(buffer.AsMemory(0, l));
                    }
                }, ct);

                await fpc.WaitForExitAsync(ct).ConfigureAwait(false);
                await Task.WhenAll(writer, reader);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                await output.FlushAsync(ct);
                stdin.Close();
                stdout.Close();
            }
            return new()
            {
                stdout = output.ToArray(),
                exitcode = fpc.ExitCode,
                message = fpc.StandardError.ReadToEnd()
            };
        }

        public static async Task<ComponentResult> ConvertMediaStdOut(string input, string format, string innerargs = null, string args = null, CancellationToken ct = default)
        {
            using Process fpc = Process.Start(new ProcessStartInfo()
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel error -y {innerargs} -i \"{input}\" {args} -f {format} -",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });

            using Stream stdout = fpc.StandardOutput.BaseStream;
            using MemoryStream output = new();
            try
            {
                await Task.Run(async () => {
                    byte[] buffer = new byte[8192];

                    int l;
                    while ((l = await stdout.ReadAsync(buffer)) > 0)
                    {
                        await output.WriteAsync(buffer.AsMemory(0, l));
                    }
                }, ct);
                await fpc.WaitForExitAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                await output.FlushAsync(ct);
                stdout.Close();
            }
            return new()
            {
                exitcode = fpc.ExitCode,
                message = fpc.StandardError.ReadToEnd(),
                stdout = output.ToArray(),
            };
        }

        public static async Task<ComponentResult> ConvertWebMedia(string input, string output, string innerargs = null, string args = null)
        {
            using Process fpc = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Process.Start(new ProcessStartInfo()
                {
                    FileName = "cmd",
                    Arguments = $"/C \"\".core/components/yt-dlp\" -o - \"{input}\" | ffmpeg -hide_banner -loglevel panic {innerargs} -i pipe:0 {args} {output}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                })
                : Process.Start(new ProcessStartInfo()
                {
                    FileName = "bash",
                    Arguments = $"-c \".core/components/yt-dlp -o - \\\"{input}\\\" | ffmpeg -hide_banner -loglevel panic {innerargs} -i pipe:0 {args} {output}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                });
            await fpc.WaitForExitAsync().ConfigureAwait(false);
            return new()
            {
                exitcode = fpc.ExitCode,
                message = fpc.StandardError.ReadToEnd()
            };
        }

        /// <summary>
        /// Method that downloads secured videos from various websites
        /// </summary>
        public static async Task<ComponentResult> DownloadVideo(string input, string innerargs = null, string args = null)
        {
            using Process fpc = Process.Start(new ProcessStartInfo()
            {
                FileName = ".core/components/yt-dlp",
                Arguments = $"{innerargs} \"{input}\" {args} -o -",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            using Stream stdout = fpc.StandardOutput.BaseStream;
            using MemoryStream output = new();
            try
            {
                await Task.Run(async () => {
                    byte[] buffer = new byte[8192];

                    int l;
                    while ((l = await stdout.ReadAsync(buffer)) > 0)
                    {
                        await output.WriteAsync(buffer.AsMemory(0, l));
                    }
                });
                await fpc.WaitForExitAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                await output.FlushAsync();
                stdout.Close();
            }
            return new()
            {
                exitcode = fpc.ExitCode,
                message = fpc.StandardError.ReadToEnd(),
                stdout = output.ToArray(),
            };
        }

        public static async Task<ComponentResult> RenderCorrscopeVideo(string input = null, string o = null, CancellationToken ct = default)
        {
            using Process fpc = Process.Start(new ProcessStartInfo()
            {
                FileName = "corr",
                Arguments = $"\"{input}\" -r \"{o}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            await fpc.WaitForExitAsync(ct).ConfigureAwait(false);
            return new()
            {
                exitcode = fpc.ExitCode,
                message = fpc.StandardError.ReadToEnd()
            };
        }

        public static async Task<ComponentResult> Sid2Wav(string directory, string input = null, string o = null, string innerargs = null, CancellationToken ct = default)
        {
            using Process fpc = Process.Start(new ProcessStartInfo()
            {
                FileName = "sidplayfp",
                Arguments = $"{innerargs} -w\"{o}\" \"{input}\"",
                UseShellExecute = false,
                WorkingDirectory = directory,
            });
            await fpc.WaitForExitAsync(ct).ConfigureAwait(false);
            return new()
            {
                exitcode = fpc.ExitCode,
            };
        }

        public static async Task<ComponentResult> VGMSplit(string path, string outputpath, uint subsong = 0, bool dontsplit = true, CancellationToken ct = default)
        {
            using Process fpc = Process.Start(new ProcessStartInfo()
            {
                FileName = "vgmsplit",
                Arguments = $"{(dontsplit ? " --no-parallel" : null)} \"{path}\" {subsong + 1}",
                UseShellExecute = false,
                WorkingDirectory = outputpath,
            });
            await fpc.WaitForExitAsync(ct).ConfigureAwait(false);
            return new()
            {
                exitcode = fpc.ExitCode,
            };
        }

        public static async Task<ComponentResult> MidiToAudio(string input, string path, CancellationToken ct = default)
        {
            using Process fpc = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Process.Start(new ProcessStartInfo()
                {
                    FileName = "cmd",
                    Arguments = $"/C \"timidity \"{input}\" -Ow --config-string \"soundfont .core/gm.sf2\" -o - | ffmpeg -hide_banner -loglevel error -i - \"{path}\"\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                })
                : Process.Start(new ProcessStartInfo()
                {
                    FileName = "bash",
                    Arguments = $"-c \"timidity \\\"{input}\\\" -Ow --config-string \\\"soundfont .core/gm.sf2\\\" -o - | ffmpeg -hide_banner -loglevel error -i - \\\"{path}\\\"\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                });
            using MemoryStream output = new();
            await fpc.StandardOutput.BaseStream.CopyToAsync(output, ct);
            output.Position = 0;

            await fpc.WaitForExitAsync(ct).ConfigureAwait(false);
            return new()
            {
                exitcode = fpc.ExitCode,
                message = fpc.StandardError.ReadToEnd()
            };
        }

        public static async Task<ComponentResult> MidiToAudioInternal(byte[] input, string format, CancellationToken ct = default)
        {
            using Process fpc = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Process.Start(new ProcessStartInfo()
                {
                    FileName = "cmd",
                    Arguments = $"/C \"timidity - -Ow --config-string \"soundfont .core/gm.sf2\" -o - | ffmpeg -hide_banner -loglevel error -i - -f {format}\" -",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = true
                })
                : Process.Start(new ProcessStartInfo()
                {
                    FileName = "bash",
                    Arguments = $"-c \"timidity - -Ow --config-string \"soundfont .core/gm.sf2\" -o - | ffmpeg -hide_banner -loglevel error -i - -f {format} -",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = true
                });
            using Stream stdin = fpc.StandardInput.BaseStream;
            using Stream stdout = fpc.StandardOutput.BaseStream;
            using MemoryStream output = new();

            try
            {
                Task writer = Task.Run(async () =>
                {
                    await stdin.WriteAsync(input);
                    await stdin.FlushAsync();
                    stdin.Close();
                }, ct);

                Task reader = Task.Run(async () =>
                {
                    const int chunkSize = 8192;
                    byte[] buffer = new byte[chunkSize];

                    int l;
                    while ((l = await stdout.ReadAsync(buffer)) > 0)
                    {
                        await output.WriteAsync(buffer.AsMemory(0, l));
                    }
                }, ct);

                await fpc.WaitForExitAsync(ct);
                await Task.WhenAll(writer, reader);
            }
            finally
            {
                stdin.Close();
                stdout.Close();
            }
            await fpc.WaitForExitAsync(ct).ConfigureAwait(false);
            return new()
            {
                stdout = output.ToArray(),
                exitcode = fpc.ExitCode,
                message = fpc.StandardError.ReadToEnd()
            };
        }
    }
}
