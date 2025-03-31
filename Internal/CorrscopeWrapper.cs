/*
    This is a part of DigitalOut and is licenced under MIT.
*/

using dtl.modules;

namespace dtl.Internal {
    public struct CorrscopeEntry {
        public string path;
        public string label;
        public float amplify;
    }

    public static class CorrscopeWrapper {
        public static string StripUnwanted(string f){
            string[] lines = f.Split(["\r\n","\r","\n"], StringSplitOptions.None);
            string[] s = [
                "width", 
                "height", 
                "ffmpeg_cli", 
                "path", 
                "wav_path", 
                "master_audio",
                "  label:", 
                "amplification",
                "audio_template",
                "video_template",
                "ChannelConfig",
            ];
            return string.Join("\n", lines.Where(l => !s.Any(k => l.Contains(k))).ToArray());
        }

        public static string CreateOverrides(string f, FileFormat format, CodecType codec, string masterpath, CorrscopeEntry[] channels, uint x, uint y) {
            f += $"  width: {x}\n  height: {y}\n";
            f += "ffmpeg_cli: !FFmpegOutputConfig\n" +
                "  path: \n" +
                $"  video_template: -c:v {ProcessHandler.GetHWAccelCodec(GPUDetector.GetGPUType(), format, codec)} {(format == FileFormat.mp4 
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
