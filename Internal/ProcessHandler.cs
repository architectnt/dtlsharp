/*
    This is a part of DigitalOut and is licenced under MIT.
*/

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using dtl.modules;

namespace dtl.Internal {

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
    }

    public static class ProcessHandler {
        public static string GetHWAccelCodec(GPUType gpuType, FileFormat format, CodecType ctype) => (gpuType, format, ctype) switch {
            (GPUType.NONE, FileFormat.mp4, CodecType.h264) => "libx264",
            (GPUType.NV, FileFormat.mp4, CodecType.h264) => "h264_nvenc",
            (GPUType.RADEON, FileFormat.mp4, CodecType.h264) => "h264_amf",
            (GPUType.ARC, FileFormat.mp4, CodecType.h264) => "h264_qsv",
            (GPUType.APPLESILICON, FileFormat.mp4, CodecType.h264) => "h264_videotoolbox",
            (GPUType.NONE, FileFormat.mp4, CodecType.hevc) => "libx264",
            (GPUType.NV, FileFormat.mp4, CodecType.hevc) => "h264_nvenc",
            (GPUType.RADEON, FileFormat.mp4, CodecType.hevc) => "h264_amf",
            (GPUType.ARC, FileFormat.mp4, CodecType.hevc) => "h264_qsv",
            (GPUType.APPLESILICON, FileFormat.mp4, CodecType.hevc) => "h264_videotoolbox",
            _ => "libx264"
        };

        public static string GetAudioCodec(FileFormat format) => format switch {
            FileFormat.mp3 => "mp3",
            FileFormat.ogg => "libvorbis",
            FileFormat.opus => "libopus",
            FileFormat.mp4 => "aac",
            _ => null // if you know you know
        };

        /// <summary>
        /// furnace out an audio file
        /// </summary>
        /// <returns>a furnaced wav</returns>
        public static async Task<ComponentResult> Furnace(string input, string outp, bool perchan = false, uint loops = 0, uint subsong = 0, CancellationToken ct = default) {
            using Process fpc = Process.Start(new ProcessStartInfo() {
                FileName = ".core/components/furnace",
                Arguments = $"-loglevel error -nostatus -subsong {subsong} -loops {loops} {(perchan ? "-outmode perchan" : null)} -output \"{outp}\" \"{input}\"",
                UseShellExecute = false,
            });
            await fpc.WaitForExitAsync(ct).ConfigureAwait(false);
            return new() {
                exitcode = fpc.ExitCode,
                message = "error message not available"
            };
        }

        public static async Task<ComponentResult> ConvertMediaInternal(byte[] input, string format, string innerargs = null, string args = null, CancellationToken ct = default) {
            using Process fpc = Process.Start(new ProcessStartInfo() {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel error -y {innerargs} -i - -threads {Environment.ProcessorCount} {args} -f {format} -",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });

            using MemoryStream output = new();
            using MemoryStream errorOutput = new();

            try {
                Task writerTask = WriteInputAsync(fpc.StandardInput.BaseStream, input);
                Task outputReader = CopyStreamAsync(fpc.StandardOutput.BaseStream, output);
                Task errorReader = CopyStreamAsync(fpc.StandardError.BaseStream, errorOutput);

                await writerTask;
                await Task.WhenAll(
                    fpc.WaitForExitAsync(),
                    outputReader,
                    errorReader
                );
            } catch (Exception ex) {
                Console.WriteLine($"Error during processing: {ex}");
                if (!fpc.HasExited)
                    try { fpc.Kill(); } catch {}
            }

            return new ComponentResult {
                stdout = output.ToArray(),
                exitcode = fpc.ExitCode,
                message = Encoding.UTF8.GetString(errorOutput.ToArray())
            };
        }

        public static async Task<ComponentResult> ConvertMediaStdOut(string input, string format, string innerargs = null, string args = null, CancellationToken ct = default) {
            using Process fpc = Process.Start(new ProcessStartInfo() {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel error -y {innerargs} -i \"{input}\" {args} -f {format} -",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });

            using MemoryStream output = new();
            using MemoryStream errorOutput = new();

            try {
                Task outputReader = CopyStreamAsync(fpc.StandardOutput.BaseStream, output);
                Task errorReader = CopyStreamAsync(fpc.StandardError.BaseStream, errorOutput);
                await Task.WhenAll(
                    fpc.WaitForExitAsync(),
                    outputReader,
                    errorReader
                );
            } catch (Exception ex) {
                Console.WriteLine($"Error during processing: {ex}");
                if (!fpc.HasExited)
                    try { fpc.Kill(); } catch {}
            }

            return new ComponentResult {
                stdout = output.ToArray(),
                exitcode = fpc.ExitCode,
                message = Encoding.UTF8.GetString(errorOutput.ToArray())
            };
        }

        public static async Task<ComponentResult> RenderCorrscopeVideo(string input = null, string o = null, CancellationToken ct = default) {
            using Process fpc = Process.Start(new ProcessStartInfo() {
                FileName = "corr",
                Arguments = $"\"{input}\" -r \"{o}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            await fpc.WaitForExitAsync(ct).ConfigureAwait(false);
            return new() {
                exitcode = fpc.ExitCode,
                message = fpc.StandardError.ReadToEnd()
            };
        }

        public static async Task<ComponentResult> Sid2Wav(string directory, string input = null, string o = null, string innerargs = null, CancellationToken ct = default) {
            using Process fpc = Process.Start(new ProcessStartInfo() {
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

        public static async Task<ComponentResult> VGMSplit(string path, string outputpath, uint subsong = 0, bool dontsplit = true, CancellationToken ct = default) {
            using Process fpc = Process.Start(new ProcessStartInfo() {
                FileName = "vgmsplit",
                Arguments = $"{(dontsplit ? " --no-parallel" : null)} \"{path}\" {subsong + 1}",
                UseShellExecute = false,
                WorkingDirectory = outputpath,
            });
            await fpc.WaitForExitAsync(ct).ConfigureAwait(false);
            return new() {
                exitcode = fpc.ExitCode,
            };
        }

        public static async Task<ComponentResult> MPTSplit(string path, string outputpath, uint loops = 0, bool dontsplit = true, CancellationToken ct = default) {
            using Process fpc = Process.Start(new ProcessStartInfo() {
                FileName = "mptsplit",
                Arguments = $"-i \"{path}\" -ofn {outputpath} {(dontsplit ? " -master" : null)} {(loops > 0 ? $"-loops {loops}" : null)}",
                UseShellExecute = false,
            });
            await fpc.WaitForExitAsync(ct).ConfigureAwait(false);
            return new() {
                exitcode = fpc.ExitCode,
            };
        }

        public static async Task<ComponentResult> MidiToAudio(string input, string path, CancellationToken ct = default) {
            using Process fpc = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Process.Start(new ProcessStartInfo()
                {
                    FileName = "cmd",
                    Arguments = $"/C \"timidity \"{input}\" -Ow --config-file=\".core/timidity.cfg\" -o - | ffmpeg -hide_banner -loglevel error -i - \"{path}\"\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                })
                : Process.Start(new ProcessStartInfo()
                { // your absolute paths requirement sucks
                    FileName = "bash",
                    Arguments = $"-c \"timidity \\\"{input}\\\" -Ow --config-file=\\\".core/timidity.cfg\\\" -o - | ffmpeg -hide_banner -loglevel error -i - \\\"{path}\\\"\"",
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

        public static async Task<ComponentResult> MidiToAudioInternal(byte[] input, string format, CancellationToken ct = default) {
            using Process fpc = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Process.Start(new ProcessStartInfo() {
                    FileName = "cmd",
                    Arguments = $"/C \"timidity - -Ow --config-file=\".core/timidity.cfg\" -o - | ./ffmpeg -hide_banner -loglevel error -i - -f {format}\" -",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = true
                }) : Process.Start(new ProcessStartInfo() {
                    FileName = "bash",
                    Arguments = $"-c \"timidity - -Ow --config-file=\\\".core/timidity.cfg\\\" -o - | ./ffmpeg -hide_banner -loglevel error -i - -f {format} -",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = true
                });

            using MemoryStream output = new();
            using MemoryStream errorOutput = new();

            try {
                Task writerTask = WriteInputAsync(fpc.StandardInput.BaseStream, input);
                Task outputReader = CopyStreamAsync(fpc.StandardOutput.BaseStream, output);
                Task errorReader = CopyStreamAsync(fpc.StandardError.BaseStream, errorOutput);

                await writerTask;
                await Task.WhenAll(
                    fpc.WaitForExitAsync(),
                    outputReader,
                    errorReader
                );
            } catch (Exception ex) {
                Console.WriteLine($"Error during processing: {ex}");
                if (!fpc.HasExited)
                    try { fpc.Kill(); } catch {}
            }

            return new ComponentResult {
                stdout = output.ToArray(),
                exitcode = fpc.ExitCode,
                message = Encoding.UTF8.GetString(errorOutput.ToArray())
            };
        }

        private static async Task WriteInputAsync(Stream stdin, byte[] input) {
            try {
                await stdin.WriteAsync(input);
                await stdin.FlushAsync();
            } finally { stdin.Close(); }
        }

        private static async Task CopyStreamAsync(Stream source, MemoryStream destination) { // Oh My GOD Kill ME I N  S I  D  E
            byte[] buffer = new byte[8192];
            int bytesRead;
            
            while ((bytesRead = await source.ReadAsync(buffer)) > 0)
                await destination.WriteAsync(buffer.AsMemory(0, bytesRead));
        }
    }
}
