using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PictureSpider
{
    public class GoogleDriveResourceUnavailableException : HttpRequestException
    {
        public GoogleDriveResourceUnavailableException(HttpStatusCode statusCode, string responseContent)
            : base($"Google Drive HTTP {(int)statusCode}: {responseContent}", null, statusCode)
        {
        }
    }

    public class GoogleDriveDownloadQueue:BaseDownloadQueue, IDisposable
    {
        private HttpClient httpClient;
        private List<Task> downloading = new List<Task>();
        private SemaphoreSlim downloadLock;

        public GoogleDriveDownloadQueue(string proxy, string apiKey, int threads = 1)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(threads);
            downloadLock = new SemaphoreSlim(threads, threads);
            httpClient = new HttpClient(new HttpClientHandler
            {
                Proxy = new WebProxy(proxy, false)
            });
            httpClient.DefaultRequestHeaders.Add("X-Goog-Api-Key", apiKey);
        }

        public override Task<bool> Add(string url, string dir, string file_name)
        {
            try
            {
                var (fileId, resourceKey) = ParseFileLink(url);
                ArgumentException.ThrowIfNullOrWhiteSpace(file_name);
                var path = Path.GetFullPath(Path.Combine(dir, file_name));
                lock (downloading)
                    downloading.Add(DownloadTask(fileId, path, resourceKey));
                return Task.FromResult(true);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"[GoogleDrive] Fail to queue: {e.Message}");
                return Task.FromResult(false);
            }
        }

        private static (string fileId, string resourceKey) ParseFileLink(string url)
        {
            string fileId = url;
            string resourceKey = null;
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                if (uri.Scheme != Uri.UriSchemeHttps || uri.Host != "drive.google.com")
                    throw new ArgumentException("Unsupported Google Drive URL.");
                var query = HttpUtility.ParseQueryString(uri.Query);
                resourceKey = query["resourcekey"];
                var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3 && parts[0] == "file" && parts[1] == "d")
                    fileId = parts[2];
                else if (uri.AbsolutePath == "/open" || uri.AbsolutePath == "/uc")
                    fileId = query["id"];
                else
                    throw new ArgumentException("Only individual Google Drive files are supported.");
            }
            if (string.IsNullOrWhiteSpace(fileId) || !Regex.IsMatch(fileId, @"\A[A-Za-z0-9_-]+\z"))
                throw new ArgumentException("Invalid Google Drive file ID.");
            return (fileId, resourceKey);
        }

        public async Task<(string id, string name)> GetFileInfoAsync(string url)
        {
            var (fileId, resourceKey) = ParseFileLink(url);
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(fileId)}?fields=id,name&supportsAllDrives=true");
            if (!string.IsNullOrWhiteSpace(resourceKey))
                request.Headers.Add("X-Goog-Drive-Resource-Keys", $"{fileId}/{resourceKey}");
            using var response = await httpClient.SendAsync(request).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    var error = JObject.Parse(content)["error"] as JObject;
                    if (response.StatusCode == HttpStatusCode.NotFound &&
                        error?.Value<int?>("code") == (int)HttpStatusCode.NotFound &&
                        error.Value<string>("message")?.StartsWith("File not found:", StringComparison.Ordinal) == true &&
                        error["errors"] is JArray errors && errors.Any(x =>
                            x is JObject detail && detail.Value<string>("reason") == "notFound" &&
                            detail.Value<string>("message")?.StartsWith("File not found:", StringComparison.Ordinal) == true))
                        throw new GoogleDriveResourceUnavailableException(response.StatusCode, content);
                }
                catch (JsonException)
                {
                }
                throw new HttpRequestException($"Google Drive HTTP {(int)response.StatusCode}: {content}",
                    null, response.StatusCode);
            }
            var file = JObject.Parse(content);
            var name = file.Value<string>("name");
            if (string.IsNullOrWhiteSpace(name) || file.Value<string>("id") != fileId)
                throw new IOException("Invalid Google Drive file metadata.");
            return (fileId, name);
        }

        public override async Task WaitForAll()
        {
            Task[] tasks;
            lock (downloading)
                tasks = downloading.ToArray();
            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch
            {
                // DownloadTask has already logged each failure.
            }
            finally
            {
                lock (downloading)
                    foreach (var task in tasks)
                        downloading.Remove(task);
            }
        }

        private async Task DownloadTask(string fileId, string path, string resourceKey)
        {
            await downloadLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (File.Exists(path))
                    return;
                using var timeout = new CancellationTokenSource(TimeSpan.FromHours(1));
                await DownloadAsync(fileId, path, resourceKey, timeout.Token).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"[GoogleDrive] Fail to download {fileId}: {e.Message}");
                throw;
            }
            finally
            {
                downloadLock.Release();
            }
        }

        // https://developers.google.com/workspace/drive/api/guides/manage-downloads
        private async Task DownloadAsync(string fileId, string path, string resourceKey, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(fileId)}?alt=media&supportsAllDrives=true");
            if (!string.IsNullOrWhiteSpace(resourceKey))
                request.Headers.Add("X-Goog-Drive-Resource-Keys", $"{fileId}/{resourceKey}");

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new HttpRequestException($"Google Drive HTTP {(int)response.StatusCode}: {error}",
                    null, response.StatusCode);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".gdrive.part";
            try
            {
                // Only expose the final file after the response has been copied completely.
                await using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 81920, FileOptions.Asynchronous))
                {
                    await response.Content.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                    if (response.Content.Headers.ContentLength is long length && output.Length != length)
                        throw new IOException($"Google Drive size mismatch: expected {length}, received {output.Length}.");
                }
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporaryPath, path);
            }
            finally
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"[GoogleDrive] Failed to remove temporary file: {e.Message}");
                }
            }
        }

        public void Dispose()
        {
            httpClient.Dispose();
            downloadLock.Dispose();
        }
    }
}
