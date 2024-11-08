/*
    This is a part of fur2mp3 Rewrite and is licenced under MIT.
*/

using System.Diagnostics;

namespace fur2mp3.Internal {
    public enum GPUType {
        NONE,
        NV,
        RADEON,
        ARC,
        MALI,
        ADRENO,
        VIDEOCORE,
        APPLESILICON,
    }
    
    public class GPUDetector {
        public static GPUType GetGPUType() {
            string gpuInfo = string.Empty;
            if (OperatingSystem.IsWindows())
                gpuInfo = RunCommand("wmic", "path win32_videocontroller get name");
            else if (OperatingSystem.IsLinux())
                gpuInfo = RunCommand("cat", "/proc/cpuinfo").Contains("Raspberry Pi") 
                    ? RunCommand("vcgencmd", "version")
                    : RunCommand("lshw", "-C display");
            else if (OperatingSystem.IsMacOS())
                gpuInfo = RunCommand("system_profiler", "SPDisplaysDataType");

            if (gpuInfo.Contains("NVIDIA"))
                return GPUType.NV;
            else if (gpuInfo.Contains("AMD") || gpuInfo.Contains("Radeon"))
                return GPUType.RADEON;
            else if (gpuInfo.Contains("Intel"))
                return GPUType.ARC;
            else if (gpuInfo.Contains("Mali"))
                return GPUType.MALI;
            else if (gpuInfo.Contains("Adreno"))
                return GPUType.ADRENO;
            else if (gpuInfo.Contains("Apple M"))
                return GPUType.APPLESILICON;
            else if (gpuInfo.Contains("VideoCore") || gpuInfo.Contains("Broadcom"))
                return GPUType.VIDEOCORE;
            else
                return GPUType.NONE;
        }

        static string RunCommand(string command, string arguments) {
            using Process process = Process.Start(new ProcessStartInfo() {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            return process?.StandardOutput.ReadToEnd() ?? string.Empty;
        }
    }
}
