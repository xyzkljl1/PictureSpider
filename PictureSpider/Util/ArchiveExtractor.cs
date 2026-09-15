using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace PictureSpider
{
    public static class ArchiveExtractor
    {
        public const long MaxArchiveBytes = 2048L * 1024 * 1024;
        private const int MaxEntryCount = 2000;
        private const long MaxSingleFileBytes = 256L * 1024 * 1024;
        private const long MaxExtractedBytes = 4096L * 1024 * 1024;
        private const int MaxCompressionRatio = 200;
        private const string CompleteMarker = ".picturespider-complete";
        private static readonly Encoding LegacyArchiveEncoding = CodePagesEncodingProvider.Instance
            .GetEncoding("GB18030", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        private static readonly HashSet<string> ReservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        public static async Task<(bool success, List<string> files)> ExtractFiles(string archivePath, string destinationDirectory,
            IEnumerable<string> allowedExtensions, CancellationToken cancellationToken = default)
        {
            string stagingRoot = null;
            try
            {
                ArgumentNullException.ThrowIfNull(allowedExtensions);
                var extensions = new HashSet<string>(allowedExtensions, StringComparer.OrdinalIgnoreCase);
                if (extensions.Count == 0)
                    throw new ArgumentException("At least one allowed extension is required.", nameof(allowedExtensions));
                bool IsAllowedFile(string path) => extensions.Contains(Path.GetExtension(path));

                if (File.Exists(Path.Combine(destinationDirectory, CompleteMarker)))
                    return (true, Directory.GetFiles(destinationDirectory, "*",
                        new EnumerationOptions { RecurseSubdirectories = true }).Where(IsAllowedFile).ToList());
                if (!File.Exists(archivePath))
                    throw new FileNotFoundException("Archive file does not exist.", archivePath);
                if (Directory.Exists(destinationDirectory))
                    throw new IOException("Incomplete archive extraction directory already exists.");
                var archiveLength = new FileInfo(archivePath).Length;
                if (archiveLength <= 0 || archiveLength > MaxArchiveBytes)
                    throw new InvalidDataException("Archive size is outside the configured limit.");

                var destinationRoot = Path.GetFullPath(destinationDirectory);
                stagingRoot = destinationRoot + ".extracting-" + Guid.NewGuid().ToString("N");
                Directory.CreateDirectory(stagingRoot);
                var extractedFiles = new List<string>();
                var destinationPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                long declaredTotal = 0, actualTotal = 0;
                var options = new ReaderOptions { ArchiveEncoding = new ArchiveEncoding { Default = LegacyArchiveEncoding } };
                using var archive = String.Equals(Path.GetExtension(archivePath), ".7z", StringComparison.OrdinalIgnoreCase)
                    ? ArchiveFactory.OpenArchive(archivePath, options) : null;
                using var reader = archive?.ExtractAllEntries() ?? ReaderFactory.OpenReader(archivePath, options);
                int entryCount = 0;
                while (reader.MoveToNextEntry())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (++entryCount > MaxEntryCount)
                        throw new InvalidDataException("Archive contains too many entries.");
                    var entry = reader.Entry;
                    if (entry.IsDirectory)
                        continue;
                    if (!IsAllowedFile(entry.Key))
                        continue;
                    if (!String.IsNullOrEmpty(entry.LinkTarget) || entry.IsEncrypted || entry.IsSplitAfter)
                        throw new InvalidDataException("Archive entry type is not supported.");
                    if (entry.Size < 0 || entry.CompressedSize < 0 || entry.Size > MaxSingleFileBytes)
                        throw new InvalidDataException("Archive entry exceeds the single-file size limit.");
                    if (entry.Size > 0 && entry.CompressedSize > 0 &&
                        entry.Size > checked(entry.CompressedSize * (long)MaxCompressionRatio))
                        throw new InvalidDataException("Archive entry exceeds the compression ratio limit.");
                    declaredTotal = checked(declaredTotal + entry.Size);
                    if (declaredTotal > MaxExtractedBytes)
                        throw new InvalidDataException("Archive exceeds the total extracted size limit.");
                    var destinationPath = GetSafeDestinationPath(stagingRoot, entry.Key);
                    if (!destinationPaths.Add(destinationPath))
                        throw new InvalidDataException("Archive contains duplicate destination paths.");
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                    long written = 0;
                    using (var input = reader.OpenEntryStream())
                    using (var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                    {
                        var buffer = new byte[81920];
                        while (true)
                        {
                            var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                            if (read == 0)
                                break;
                            written = checked(written + read);
                            actualTotal = checked(actualTotal + read);
                            if (written > MaxSingleFileBytes || actualTotal > MaxExtractedBytes)
                                throw new InvalidDataException("Archive exceeded the extraction size limit.");
                            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                        }
                    }
                    if (entry.Size > 0 && written != entry.Size)
                        throw new InvalidDataException("Archive entry length does not match the directory entry.");
                    extractedFiles.Add(destinationPath);
                }
                if (extractedFiles.Count == 0)
                    throw new InvalidDataException("Archive does not contain supported files.");
                await File.WriteAllTextAsync(Path.Combine(stagingRoot, CompleteMarker), DateTime.UtcNow.ToString("O"), cancellationToken);
                Directory.Move(stagingRoot, destinationRoot);
                return (true, extractedFiles.Select(x =>
                    Path.Combine(destinationRoot, Path.GetRelativePath(stagingRoot, x))).ToList());
            }
            catch (Exception e)
            {
                if (!String.IsNullOrEmpty(stagingRoot) && Directory.Exists(stagingRoot))
                {
                    try
                    {
                        Directory.Delete(stagingRoot, true);
                    }
                    catch (Exception cleanupException)
                    {
                        Console.Error.WriteLine($"[ArchiveExtractor] Failed to remove extraction directory: {cleanupException.Message}");
                    }
                }
                Console.Error.WriteLine($"[ArchiveExtractor] Failed to extract {archivePath}: {e.Message}");
                return (false, null);
            }
        }

        private static string GetSafeDestinationPath(string root, string entryName)
        {
            if (String.IsNullOrWhiteSpace(entryName) || Path.IsPathRooted(entryName) || entryName.Contains(':') || entryName.Contains('\0'))
                throw new InvalidDataException("Invalid archive entry path.");
            var normalized = entryName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            foreach (var part in normalized.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            {
                if (part == "." || part == ".." || part != part.TrimEnd(' ', '.'))
                    throw new InvalidDataException("Invalid archive entry path segment.");
                if (ReservedNames.Contains(Path.GetFileNameWithoutExtension(part)))
                    throw new InvalidDataException("Reserved Windows file name in archive.");
            }
            var fullRoot = Path.GetFullPath(root);
            if (!fullRoot.EndsWith(Path.DirectorySeparatorChar))
                fullRoot += Path.DirectorySeparatorChar;
            var destinationPath = Path.GetFullPath(Path.Combine(fullRoot, normalized));
            if (!destinationPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Archive entry points outside the extraction directory.");
            return destinationPath;
        }
    }
}
