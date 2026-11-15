using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace PictureSpider.Pawchive
{
    public class RemoteArchiveEntry
    {
        public string FullName { get; set; }
        public long CompressedLength { get; set; }
        public long Length { get; set; }
        public bool IsDirectory { get; set; }
        public bool IsEncrypted { get; set; }
    }

    public class HttpArchiveEntriesReader : IDisposable
    {
        private const string BrowserUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/148.0.0.0 Safari/537.36";
        private const int RequestTimeoutSeconds = 35;

        // ZIP EOCD 由 22 字节固定字段和最长 65535 字节注释组成。
        private const int EocdFixedLength = 22;
        private const int MaxZipCommentLength = UInt16.MaxValue;
        private const int MaxEocdLength = EocdFixedLength + MaxZipCommentLength;
        // 限制只用于解析 key.zip 的目录元数据，避免异常目录消耗过多内存。
        private const int MaxCentralDirectoryLength = 256 * 1024;
        private const int MaxEntryCount = 4096;
        // ZIP 结构签名按小端序读取，分别对应 PK\x05\x06 和 PK\x01\x02。
        private const uint EocdSignature = 0x06054b50;
        private const uint CentralDirectoryHeaderSignature = 0x02014b50;
        private const int CentralDirectoryHeaderLength = 46;
        // ZIP general purpose bit flag 的第 11 位表示文件名使用 UTF-8。
        private const ushort Utf8FileNameFlag = 1 << 11;

        // 以下偏移量来自 ZIP EOCD 固定字段布局。
        private const int EocdDiskNumberOffset = 4;
        private const int EocdCentralDirectoryDiskOffset = 6;
        private const int EocdEntryCountOnDiskOffset = 8;
        private const int EocdEntryCountOffset = 10;
        private const int EocdCentralDirectoryLengthOffset = 12;
        private const int EocdCentralDirectoryPositionOffset = 16;
        private const int EocdCommentLengthOffset = 20;

        // 以下偏移量来自 ZIP central directory file header 固定字段布局。
        private const int HeaderFlagsOffset = 8;
        private const int HeaderCompressedLengthOffset = 20;
        private const int HeaderLengthOffset = 24;
        private const int HeaderNameLengthOffset = 28;
        private const int HeaderExtraLengthOffset = 30;
        private const int HeaderCommentLengthOffset = 32;
        private const byte PrintableAsciiStart = 0x20;
        private const byte PrintableAsciiEnd = 0x7e;
        private readonly HttpClient httpClient;
        private readonly string allowedHost;

        public HttpArchiveEntriesReader(string proxy, string _allowedHost)
        {
            allowedHost = _allowedHost;
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                Proxy = String.IsNullOrEmpty(proxy) ? null : new WebProxy(proxy, false),
                UseProxy = !String.IsNullOrEmpty(proxy),
                UseCookies = false
            };
            httpClient = new HttpClient(handler);
            httpClient.Timeout = TimeSpan.FromSeconds(RequestTimeoutSeconds);
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
        }

        public void Dispose()
        {
            httpClient.Dispose();
        }

        public async Task<(bool success, List<RemoteArchiveEntry> entries)> GetEntries(string url, string extension = ".zip",
            CancellationToken cancellationToken = default)
        {
            try
            {
                extension = extension.ToLowerInvariant();
                if (extension is not (".zip" or ".rar" or ".7z"))
                {
                    Console.Error.WriteLine($"[HttpArchiveEntriesReader] Unsupported archive extension: {extension}");
                    return (false, null);
                }
                return (true, await ReadEntries(url, extension, cancellationToken));
            }
            catch (Exception e) when (e is IOException or InvalidDataException or HttpRequestException or OperationCanceledException
                or ArgumentException or NotSupportedException or SharpCompressException or OverflowException)
            {
                Console.Error.WriteLine($"[HttpArchiveEntriesReader] Failed to read {url}: {e.Message}");
                return (false, null);
            }
        }

        private async Task<List<RemoteArchiveEntry>> ReadEntries(string url, string extension, CancellationToken cancellationToken)
        {
            var uri = new Uri(url);
            ValidateUri(uri);
            long contentLength;
            EntityTagHeaderValue etag;
            DateTimeOffset? lastModified;
            using (var request = new HttpRequestMessage(HttpMethod.Head, uri))
            {
                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (response.StatusCode != HttpStatusCode.OK)
                    throw new HttpRequestException($"Remote archive HEAD failed: {(int)response.StatusCode}", null, response.StatusCode);
                ValidateUri(response.RequestMessage.RequestUri);
                if (!response.Headers.AcceptRanges.Contains("bytes"))
                    throw new IOException("Remote archive does not advertise byte range support.");
                contentLength = response.Content.Headers.ContentLength ?? -1;
                if (contentLength <= EocdFixedLength)
                    throw new InvalidDataException("Remote archive is too small.");
                etag = response.Headers.ETag;
                lastModified = response.Content.Headers.LastModified;
            }

            async Task<byte[]> ReadRemoteRange(long from, long to, CancellationToken token)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                if (etag is not null && !etag.IsWeak)
                    request.Headers.IfRange = new RangeConditionHeaderValue(etag);
                else if (lastModified.HasValue)
                    request.Headers.IfRange = new RangeConditionHeaderValue(lastModified.Value);
                return await ReadRange(httpClient, request, from, to, contentLength, token,
                    response => ValidateUri(response.RequestMessage.RequestUri));
            }
            if (extension == ".zip")
                return await ReadEntries(contentLength, ReadRemoteRange, cancellationToken);
            // SharpCompress 列目录使用同步的可定位流，放到线程池避免阻塞调用线程。
            return await Task.Run(() => ReadArchiveEntries(contentLength, extension, ReadRemoteRange, cancellationToken), cancellationToken);
        }

        internal static List<RemoteArchiveEntry> ReadArchiveEntries(long contentLength, string extension,
            Func<long, long, CancellationToken, Task<byte[]>> readRange, CancellationToken cancellationToken = default)
        {
            using var stream = new HttpRangeStream(contentLength, readRange, cancellationToken);
            var options = new ReaderOptions
            {
                ArchiveEncoding = new ArchiveEncoding
                {
                    Default = CodePagesEncodingProvider.Instance.GetEncoding("GB18030",
                        EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)
                }
            };
            using IArchive archive = extension switch
            {
                ".rar" => RarArchive.OpenArchive(stream, options),
                ".7z" => SevenZipArchive.OpenArchive(stream, options),
                _ => throw new NotSupportedException("Unsupported remote archive format.")
            };
            var result = new List<RemoteArchiveEntry>();
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (result.Count >= MaxEntryCount)
                    throw new InvalidDataException("Remote archive contains too many entries.");
                if (!entry.IsComplete || entry.IsSplitAfter || !String.IsNullOrEmpty(entry.LinkTarget))
                    throw new InvalidDataException("Split or linked archive entries are not supported.");
                result.Add(new RemoteArchiveEntry
                {
                    FullName = entry.Key,
                    CompressedLength = entry.CompressedSize,
                    Length = entry.Size,
                    IsDirectory = entry.IsDirectory,
                    IsEncrypted = entry.IsEncrypted
                });
            }
            if (result.Count > 0 && archive is IRarArchive rar && rar.IsMultipartVolume())
                throw new InvalidDataException("Multi-volume RAR archives are not supported.");
            return result;
        }

        internal static async Task<List<RemoteArchiveEntry>> ReadEntries(long contentLength,
            Func<long, long, CancellationToken, Task<byte[]>> readRange, CancellationToken cancellationToken = default)
        {
            if (contentLength <= EocdFixedLength)
                throw new InvalidDataException("Remote ZIP is too small.");
            // 至少保留一个字节不读取，禁止在服务端文件很小时退化为完整下载。
            var tailLength = (int)Math.Min(MaxEocdLength, contentLength - 1);
            var tailStart = contentLength - tailLength;
            var tail = await readRange(tailStart, contentLength - 1, cancellationToken);
            var eocdOffset = FindEocd(tail);
            if (eocdOffset < 0)
                throw new InvalidDataException("ZIP end of central directory was not found.");

            var diskNumber = ReadUInt16(tail, eocdOffset + EocdDiskNumberOffset);
            var centralDirectoryDisk = ReadUInt16(tail, eocdOffset + EocdCentralDirectoryDiskOffset);
            var entryCountOnDisk = ReadUInt16(tail, eocdOffset + EocdEntryCountOnDiskOffset);
            var entryCount = ReadUInt16(tail, eocdOffset + EocdEntryCountOffset);
            var centralDirectoryLength = ReadUInt32(tail, eocdOffset + EocdCentralDirectoryLengthOffset);
            var centralDirectoryOffset = ReadUInt32(tail, eocdOffset + EocdCentralDirectoryPositionOffset);
            if (diskNumber != 0 || centralDirectoryDisk != 0 || entryCountOnDisk != entryCount)
                throw new NotSupportedException("Multi-volume ZIP files are not supported.");
            if (entryCount == UInt16.MaxValue || centralDirectoryLength == UInt32.MaxValue || centralDirectoryOffset == UInt32.MaxValue)
                throw new NotSupportedException("ZIP64 key files are not supported.");
            if (entryCount > MaxEntryCount || centralDirectoryLength > MaxCentralDirectoryLength)
                throw new InvalidDataException("Remote ZIP directory exceeds the configured metadata limits.");
            var centralDirectoryEnd = checked((long)centralDirectoryOffset + centralDirectoryLength);
            if (centralDirectoryEnd > contentLength || centralDirectoryEnd > tailStart + eocdOffset)
                throw new InvalidDataException("Remote ZIP directory points outside the archive.");

            byte[] centralDirectory;
            if (centralDirectoryOffset >= tailStart && centralDirectoryEnd <= tailStart + tail.Length)
            {
                centralDirectory = new byte[centralDirectoryLength];
                Array.Copy(tail, centralDirectoryOffset - tailStart, centralDirectory, 0, centralDirectoryLength);
            }
            else
            {
                if (centralDirectoryLength == 0)
                    centralDirectory = Array.Empty<byte>();
                else
                    centralDirectory = await readRange(centralDirectoryOffset, centralDirectoryEnd - 1, cancellationToken);
            }
            return ParseCentralDirectory(centralDirectory, entryCount);
        }

        internal static async Task<byte[]> ReadRange(HttpClient client, HttpRequestMessage request,
            long from, long to, long totalLength, CancellationToken cancellationToken,
            Action<HttpResponseMessage> validateResponse)
        {
            var expectedLength = checked(to - from + 1);
            if (expectedLength <= 0 || expectedLength > MaxCentralDirectoryLength)
                throw new InvalidDataException("Invalid remote archive byte range.");
            request.Headers.Range = new RangeHeaderValue(from, to);
            request.Headers.AcceptEncoding.ParseAdd("identity");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode != HttpStatusCode.PartialContent)
            {
                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException($"Remote archive HTTP {(int)response.StatusCode}", null, response.StatusCode);
                throw new IOException($"Remote archive ignored the byte range request: {(int)response.StatusCode}");
            }
            validateResponse(response);
            var contentRange = response.Content.Headers.ContentRange;
            if (contentRange is null || contentRange.From != from || contentRange.To != to || contentRange.Length != totalLength)
                throw new InvalidDataException("Remote archive returned an unexpected content range.");
            if (response.Content.Headers.ContentEncoding.Count > 0)
                throw new InvalidDataException("Encoded byte range responses are not supported.");
            if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value != expectedLength)
                throw new InvalidDataException("Remote archive returned an unexpected range length.");

            var result = new byte[expectedLength];
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await stream.ReadExactlyAsync(result, cancellationToken);
            var extra = new byte[1];
            if (await stream.ReadAsync(extra.AsMemory(0, 1), cancellationToken) != 0)
                throw new InvalidDataException("Remote archive range exceeded the requested length.");
            return result;
        }

        private void ValidateUri(Uri uri)
        {
            if (uri is null || uri.Scheme != Uri.UriSchemeHttps || !uri.Host.Equals(allowedHost, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Unexpected remote archive host.");
        }

        private static int FindEocd(byte[] data)
        {
            for (var offset = data.Length - EocdFixedLength; offset >= 0; --offset)
                if (ReadUInt32(data, offset) == EocdSignature)
                {
                    var commentLength = ReadUInt16(data, offset + EocdCommentLengthOffset);
                    if (offset + EocdFixedLength + commentLength == data.Length)
                        return offset;
                }
            return -1;
        }

        private static List<RemoteArchiveEntry> ParseCentralDirectory(byte[] data, int entryCount)
        {
            var result = new List<RemoteArchiveEntry>();
            var offset = 0;
            var utf8 = new UTF8Encoding(false, true);
            for (var i = 0; i < entryCount; ++i)
            {
                EnsureAvailable(data, offset, CentralDirectoryHeaderLength);
                if (ReadUInt32(data, offset) != CentralDirectoryHeaderSignature)
                    throw new InvalidDataException("Invalid ZIP central directory entry.");
                var flags = ReadUInt16(data, offset + HeaderFlagsOffset);
                var compressedLength = ReadUInt32(data, offset + HeaderCompressedLengthOffset);
                var length = ReadUInt32(data, offset + HeaderLengthOffset);
                var nameLength = ReadUInt16(data, offset + HeaderNameLengthOffset);
                var extraLength = ReadUInt16(data, offset + HeaderExtraLengthOffset);
                var commentLength = ReadUInt16(data, offset + HeaderCommentLengthOffset);
                if (compressedLength == UInt32.MaxValue || length == UInt32.MaxValue)
                    throw new NotSupportedException("ZIP64 key file entries are not supported.");
                var totalEntryLength = checked(CentralDirectoryHeaderLength + nameLength + extraLength + commentLength);
                EnsureAvailable(data, offset, totalEntryLength);
                var nameBytes = data.AsSpan(offset + CentralDirectoryHeaderLength, nameLength);
                string name;
                if ((flags & Utf8FileNameFlag) != 0)
                    name = utf8.GetString(nameBytes);
                else
                {
                    if (nameBytes.IndexOfAnyExceptInRange(PrintableAsciiStart, PrintableAsciiEnd) >= 0)
                        throw new InvalidDataException("Non-ASCII legacy ZIP names are not supported for key files.");
                    name = Encoding.ASCII.GetString(nameBytes);
                }
                result.Add(new RemoteArchiveEntry
                {
                    FullName = name,
                    CompressedLength = compressedLength,
                    Length = length,
                    IsDirectory = name.EndsWith("/") || name.EndsWith("\\"),
                    IsEncrypted = (flags & 0x41) != 0
                });
                offset += totalEntryLength;
            }
            if (offset != data.Length)
                throw new InvalidDataException("Unexpected data after the ZIP central directory.");
            return result;
        }

        private static void EnsureAvailable(byte[] data, int offset, int length)
        {
            if (offset < 0 || length < 0 || offset > data.Length - length)
                throw new InvalidDataException("Truncated ZIP central directory.");
        }

        private static ushort ReadUInt16(byte[] data, int offset)
        {
            EnsureAvailable(data, offset, sizeof(ushort));
            return BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, sizeof(ushort)));
        }

        private static uint ReadUInt32(byte[] data, int offset)
        {
            EnsureAvailable(data, offset, sizeof(uint));
            return BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, sizeof(uint)));
        }
    }
}
