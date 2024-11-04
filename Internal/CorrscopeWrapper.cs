/*
    This is a part of fur2mp3 Rewrite and is licenced under MIT.
*/

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
        public static string CreateCorrscopeOverrides(FileFormat format, string masterpath, CorrscopeEntry[] channels, uint x, uint y)
        {
            string f = File.ReadAllText(".core/fus_osc_config.yaml");
            f += $"  width: {x}\n  height: {y}\n";
            f += "ffmpeg_cli: !FFmpegOutputConfig\n" +
                "  path: \n" +
                $"  video_template: -c:v {ProcessHandler.GetHWAccelCodec(GPUDetector.GetGPUType(), format)} {(format == FileFormat.mp4 
                    ? "-movflags +faststart+frag_keyframe+empty_moov+default_base_moof" 
                    : null)}"+
                "\n";
            f += $"  audio_template: -c:a {ProcessHandler.GetAudioCodec(format)}\n";
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
