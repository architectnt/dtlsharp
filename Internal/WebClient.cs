/*
    This is a part of fur2mp3 Rewrite and is licenced under MIT.
*/

namespace fur2mp3.Internal {
    public static class WebClient {
        readonly static HttpClient cl = new();
        public async static Task<byte[]> GetDataAsync(string url)
            => await cl.GetByteArrayAsync(url);
        public async static Task SaveDataAsync(string url, string path)
        {
            byte[] d = await cl.GetByteArrayAsync(url);
            using FileStream fs = new(path, FileMode.Create);
            await fs.WriteAsync(d);
        }

        public async static Task<string> GetText(string url)
            => await cl.GetStringAsync(url);

        public async static Task<bool> IsValidUrl(string url){
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri r) || (r.Scheme != Uri.UriSchemeHttp && r.Scheme != Uri.UriSchemeHttps)) {
                return false;
            }
            try {
                using HttpResponseMessage rs =  await cl.GetAsync(r);
                return rs.IsSuccessStatusCode;
            } catch{
                return false;
            }
        }
    }
}
