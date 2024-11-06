/*
    This is a part of fur2mp3 Rewrite and is licenced under MIT.
*/

using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace fur2mp3 {
    public static class API {
        public const string tmpdir = ".tmp";

        public static Color RedColor {
            get => new(255, 20, 75);
        }

        public static string FriendlyTimeFormat(TimeSpan elapsed) {
            if (elapsed.TotalHours >= 1)
                return $"{(int)elapsed.TotalHours}h{elapsed.Minutes}m{elapsed.Seconds}s";
            else if (elapsed.TotalMinutes >= 1)
                return $"{(int)elapsed.TotalMinutes}m{elapsed.Seconds}s";
            else return $"{elapsed.Seconds}.{elapsed.Milliseconds}s";
        }

        public static string RemoveQueryParameters(string url) {
            return url.Contains("cdn.discord") ? url.Split('?')[0] : url;
        }

        public struct DataOutput {
            public byte[] data;
            public string name;
        }
    }
}
