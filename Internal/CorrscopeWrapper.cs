using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace fur2mp3.Internal {
    public struct CorrscopeEntry
    {
        public string path;
        public string label;
        public float amplify;
    }

    public static class CorrscopeWrapper {
        public static string CreateCorrscopeOverrides(string masterpath, CorrscopeEntry[] channels, uint x, uint y)
        {
            string f = File.ReadAllText(".core/fus_osc_config.yaml");
            f += $"  width: {x}\n  height: {y}\n";

            string codec = GPUDetector.GetGPUType() switch
            {
                GPUType.NONE => "libx264",
                GPUType.NV => "h264_nvenc",
                GPUType.RADEON => "h264_amf",
                GPUType.ARC => "h264_qsv",
                _ => "libx264",
            };
            f += "ffmpeg_cli: !FFmpegOutputConfig\n" +
                "  path: \n" +
                $"  video_template: -c:v {codec} -movflags faststart\n";
            f += "channels:\n";
            for (int i = 0; i < channels.Length; i++) {
                CorrscopeEntry s = channels[i];
                f += "- !ChannelConfig\n" +
                    $"  wav_path: {s.path}\n";
                if (s.label != null) f += $"  label: {s.label}\n";
                if (s.amplify != 0) f += $"  amplification: {s.amplify}\n";
            }
            f += $"master_audio: {masterpath}\n";
            return f;
        }
    }
}
