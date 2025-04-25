/*
    This is a part of DigitalOut and is licenced under MIT.
*/

using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using dtl.Internal;

namespace dtl {
    public static class API {
        public static Dictionary<ulong, List<(byte[] dt, string name, float amp, long lastusetime)>> modulecache = []; // okay
        public static readonly Settings settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(".core/settings.json"), new JsonSerializerOptions(){
            IncludeFields = true
        });

        public const string tmpdir = ".tmp";

        public static Color RedColor => new(255, 20, 75);

        public static string FormatTime(TimeSpan elapsed) {
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
