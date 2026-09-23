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
using System.Collections.Generic;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PictureSpider
{
    //该类用于取代创建megaApiClient的WebClient以令其可以使用代理，代码基于CG.Web.MegaApiClient.WebClient修改
    public class MegaWebClient : IWebClient
    {
        private const int DefaultResponseTimeout = -1;

        private readonly HttpClient _httpClient;
        private readonly HttpClient _httpClientDownload;
        private readonly Action<TimeSpan?> bandwidthLimitExceeded;
        public static CookieContainer cookieContainer = new CookieContainer();

        public int BufferSize { get; set; } = 65536;

        public MegaWebClient(WebProxy proxy, WebProxy proxy_download, Action<TimeSpan?> _bandwidthLimitExceeded)
        {
            //CG.Web.MegaApiClient.WebClient
            _httpClient = CreateHttpClient(-1, GenerateUserAgent(), proxy);
            //_httpClientDownload = _httpClient;
            _httpClientDownload = CreateHttpClient(-1, GenerateUserAgent(), proxy_download);
            bandwidthLimitExceeded = _bandwidthLimitExceeded;
        }
        public bool isDownloadURL(Uri url)
        {
            return url.Host.Contains("userstorage.mega.co.nz");
        }

        public string PostRequestJson(Uri url, string jsonData)
        {
            using MemoryStream dataStream = new MemoryStream(Encoding.UTF8.GetBytes(jsonData));
            using Stream stream = PostRequest(url, dataStream, "application/json");
            var result = StreamToString(stream);
            var request = JArray.Parse(jsonData);
            var response = JArray.Parse(result);
            if (request.Count == 1 && request[0]["a"]?.ToString() == "g" &&
                response.Count == 1 && response[0] is JObject error &&
                error.Value<int?>("e") == (int)ApiResultCode.QuotaExceeded)
            {
                var seconds = error.Value<int?>("tl");
                bandwidthLimitExceeded?.Invoke(seconds > 0 ? TimeSpan.FromSeconds(seconds.Value) : null);
                return $"[{(int)ApiResultCode.QuotaExceeded}]";
            }
            if (!QueryHelpers.ParseQuery(url.Query).ContainsKey("n") || request.Count != 1 || request[0]["a"]?.ToString() != "f")
                return result;

            if (response.Count != 1 || response[0] is not JObject responseObject || responseObject["f"] is not JArray nodes)
                return result;

            // MegaApiClient只使用第一份节点密钥；嵌套分享可能把旧的父分享密钥放在前面。
            var nodeIds = new HashSet<string>();
            foreach (JObject node in nodes)
                nodeIds.Add(node.Value<string>("h"));
            string rootId = null;
            foreach (JObject node in nodes)
                if (node.Value<int>("t") == 1 && !nodeIds.Contains(node.Value<string>("p")))
                {
                    rootId = node.Value<string>("h");
                    break;
                }
            if (string.IsNullOrEmpty(rootId))
                return result;

            foreach (JObject node in nodes)
            {
                var keys = node.Value<string>("k")?.Split('/');
                if (keys is null || keys.Length < 2)
                    continue;
                var rootKeyIndex = Array.FindIndex(keys, x => x.StartsWith(rootId + ":", StringComparison.Ordinal));
                if (rootKeyIndex <= 0)
                    continue;
                var rootKey = keys[rootKeyIndex];
                Array.Copy(keys, 0, keys, 1, rootKeyIndex);
                keys[0] = rootKey;
                node["k"] = string.Join("/", keys);
            }
            return response.ToString(Formatting.None);
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
            var client = isDownloadURL(url) ? _httpClientDownload : _httpClient;
            var response = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
            EnsureSuccessResponse(response);
            if (isDownloadURL(url))
                return new ResumableDownloadStream(this, url, response);
            return response.Content.ReadAsStream();
        }

        private void EnsureSuccessResponse(HttpResponseMessage response)
        {
            if ((int)response.StatusCode == 509)
            {
                TimeSpan? retryAfter = null;
                if (response.Headers.TryGetValues("X-MEGA-Time-Left", out var values))
                    foreach (var value in values)
                        if (int.TryParse(value, out var seconds) && seconds > 0)
                        {
                            retryAfter = TimeSpan.FromSeconds(seconds);
                            break;
                        }
                if (!retryAfter.HasValue && response.Headers.RetryAfter?.Delta is TimeSpan delta && delta > TimeSpan.Zero)
                    retryAfter = delta;
                if (!retryAfter.HasValue && response.Headers.RetryAfter?.Date is DateTimeOffset date && date > DateTimeOffset.UtcNow)
                    retryAfter = date - DateTimeOffset.UtcNow;
                bandwidthLimitExceeded?.Invoke(retryAfter);
            }
            if (!response.IsSuccessStatusCode)
            {
                var exception = new HttpRequestException(
                    $"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase}).",
                    null, response.StatusCode);
                response.Dispose();
                throw exception;
            }
        }

        private sealed class ResumableDownloadStream : Stream
        {
            private readonly MegaWebClient webClient;
            private readonly Uri url;
            private HttpResponseMessage response;
            private Stream stream;
            private long position;
            private long? length;
            private bool disposed;

            public ResumableDownloadStream(MegaWebClient _webClient, Uri _url, HttpResponseMessage _response)
            {
                webClient = _webClient;
                url = _url;
                response = _response;
                stream = response.Content.ReadAsStream();
                length = response.Content.Headers.ContentLength;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => length ?? throw new NotSupportedException();
            public override long Position
            {
                get => position;
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                if (count == 0)
                    return 0;

                while (true)
                {
                    Exception interruption;
                    try
                    {
                        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                        var read = stream.ReadAsync(buffer, offset, count, timeout.Token).GetAwaiter().GetResult();
                        if (read > 0)
                        {
                            position += read;
                            return read;
                        }
                        if (!length.HasValue || position >= length.Value)
                            return 0;
                        interruption = new EndOfStreamException($"MEGA response ended at {position} of {length.Value} bytes.");
                    }
                    catch (OperationCanceledException e)
                    {
                        interruption = e;
                    }
                    catch (HttpRequestException e) when (!e.StatusCode.HasValue)
                    {
                        interruption = e;
                    }
                    catch (IOException e)
                    {
                        interruption = e;
                    }

                    if (!Reconnect())
                        throw new IOException($"MEGA download could not resume at byte {position}.", interruption);
                }
            }

            private bool Reconnect()
            {
                stream.Dispose();
                response.Dispose();
                stream = null;
                response = null;

                for (var attempt = 1; attempt <= 5; attempt++)
                {
                    HttpResponseMessage newResponse = null;
                    try
                    {
                        using var request = new HttpRequestMessage(HttpMethod.Get, url);
                        request.Headers.Range = new RangeHeaderValue(position, null);
                        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
                        newResponse = webClient._httpClientDownload.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).GetAwaiter().GetResult();
                        webClient.EnsureSuccessResponse(newResponse);
                        var contentRange = newResponse.Content.Headers.ContentRange;
                        if (newResponse.StatusCode != HttpStatusCode.PartialContent ||
                            contentRange?.From != position || !contentRange.Length.HasValue ||
                            length.HasValue && contentRange.Length.Value != length.Value)
                            throw new InvalidDataException("MEGA server returned an invalid range response.");

                        length = contentRange.Length.Value;
                        stream = newResponse.Content.ReadAsStream();
                        response = newResponse;
                        newResponse = null;
                        Console.WriteLine($"[Mega] Resume download at byte {position}.");
                        return true;
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (HttpRequestException e) when (!e.StatusCode.HasValue)
                    {
                    }
                    catch (IOException)
                    {
                    }
                    finally
                    {
                        newResponse?.Dispose();
                    }

                    if (attempt < 5)
                        Thread.Sleep(TimeSpan.FromSeconds(attempt * 2));
                }
                return false;
            }

            protected override void Dispose(bool disposing)
            {
                if (!disposed && disposing)
                {
                    disposed = true;
                    stream?.Dispose();
                    response?.Dispose();
                }
                base.Dispose(disposing);
            }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }

        private Stream PostRequest(Uri url, Stream dataStream, string contentType)
        {
            try
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
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw;
            }
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
