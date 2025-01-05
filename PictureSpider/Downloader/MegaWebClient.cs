using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Net.Http;
using System.Net.Http.Headers;
using CG.Web.MegaApiClient;
using System.Security.Authentication;

namespace PictureSpider
{
    //该类用于取代创建megaApiClient的WebClient以令其可以使用代理，代码基于CG.Web.MegaApiClient.WebClient修改
    public class MegaWebClient : IWebClient
    {
        private const int DefaultResponseTimeout = -1;

        private readonly HttpClient _httpClient;
        private readonly HttpClient _httpClientDownload;
        public static CookieContainer cookieContainer = new CookieContainer();

        public int BufferSize { get; set; } = 65536;

        public MegaWebClient(WebProxy proxy, WebProxy proxy_download)
        {
            _httpClient = CreateHttpClient(-1, GenerateUserAgent(), proxy);
            _httpClientDownload = CreateHttpClient(-1, GenerateUserAgent(), proxy_download);
        }
        public bool isDownloadURL(Uri url)
        {
            return url.Host.Contains("userstorage.mega.co.nz");
        }

        public string PostRequestJson(Uri url, string jsonData)
        {
            using MemoryStream dataStream = new MemoryStream(Encoding.UTF8.GetBytes(jsonData));
            using Stream stream = PostRequest(url, dataStream, "application/json");
            return StreamToString(stream);
        }

        public string PostRequestRaw(Uri url, Stream dataStream)
        {
            using Stream stream = PostRequest(url, dataStream, "application/json");
            return StreamToString(stream);
        }

        public Stream PostRequestRawAsStream(Uri url, Stream dataStream)
        {
            return PostRequest(url, dataStream, "application/octet-stream");
        }

        public Stream GetRequestRaw(Uri url)
        {
            if (isDownloadURL(url))
            {
                return _httpClientDownload.GetStreamAsync(url).Result;

            }
            else
                return _httpClient.GetStreamAsync(url).Result;
        }

        private Stream PostRequest(Uri url, Stream dataStream, string contentType)
        {
            using StreamContent streamContent = new StreamContent(dataStream, BufferSize);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = streamContent
            };
            HttpResponseMessage result;
            if (isDownloadURL(url))
                result = _httpClientDownload.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).Result;
            else
                result = _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).Result;
            if (!result.IsSuccessStatusCode && result.StatusCode == HttpStatusCode.InternalServerError && result.ReasonPhrase == "Server Too Busy")
            {
                return new MemoryStream(Encoding.UTF8.GetBytes((-3L).ToString()));
            }

            result.EnsureSuccessStatusCode();
            return result.Content.ReadAsStreamAsync().Result;
        }

        private string StreamToString(Stream stream)
        {
            using StreamReader streamReader = new StreamReader(stream, Encoding.UTF8);
            return streamReader.ReadToEnd();
        }

        private static HttpClient CreateHttpClient(int timeout, ProductInfoHeaderValue userAgent, WebProxy proxy)
        {
            return new HttpClient(new HttpClientHandler
            {
                SslProtocols = SslProtocols.Tls12,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                CookieContainer = cookieContainer,
                Proxy = proxy
            }, disposeHandler: true)
            {
                Timeout = TimeSpan.FromMilliseconds(timeout),
                DefaultRequestHeaders =
            {
                UserAgent = { userAgent }
            }
            };
        }

        private static ProductInfoHeaderValue GenerateUserAgent()
        {
            AssemblyName name = typeof(MegaWebClient).GetTypeInfo().Assembly.GetName();
            return new ProductInfoHeaderValue(name.Name, name.Version.ToString(2));
        }
    }
}
