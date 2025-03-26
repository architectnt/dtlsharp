/*
    This is a part of DigitalOut and is licenced under MIT.
*/

using CatBox.NET.Client;
using Microsoft.Extensions.DependencyInjection;

namespace dtl.Internal {
    public static class WebClient {
        readonly static HttpClient cl = new();
        public async static Task<byte[]> GetDataAsync(string url)
            => await cl.GetByteArrayAsync(url);
        public async static Task SaveDataAsync(string url, string path) {
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

        public static ILitterboxClient GetLiterBoxInstance(){
            AsyncServiceScope scope = Program.services.CreateAsyncScope();
            return scope.ServiceProvider.GetRequiredService<ILitterboxClient>();
        }
    }
}
