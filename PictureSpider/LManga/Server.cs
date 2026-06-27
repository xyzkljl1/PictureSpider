using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PictureSpider.LManga
{
    public class Server : BaseServer, IDisposable
    {
        private readonly string rootDir;

        public Server(Config config)
        {
            logPrefix = "LM";
            rootDir = config.LMangaRootDir;
            Util.TouchDir(rootDir);
        }

        public void Dispose()
        {
        }

        public override Task<List<ExplorerQueue>> GetExplorerQueues()
        {
            var result = new List<ExplorerQueue>();
            if (Directory.Exists(rootDir))
                foreach (var mangaDir in Directory.GetDirectories(rootDir).OrderBy(Path.GetFileName))
                    result.Add(new ExplorerQueue(ExplorerQueue.QueueType.Folder, Path.GetFullPath(mangaDir), $"LManga-{Path.GetFileName(mangaDir)}"));
            return Task.FromResult(result);
        }

        public override Task<List<ExplorerFileBase>> GetExplorerQueueItems(ExplorerQueue queue)
        {
            var result = new List<ExplorerFileBase>();
            if (!Directory.Exists(queue.id))
                return Task.FromResult(result);

            var mangaName = Path.GetFileName(queue.id);
            var chapterDirs = Directory.GetDirectories(queue.id).OrderBy(Path.GetFileName).ToList();
            int firstUnreadIndex = chapterDirs.FindLastIndex(chapterDir => File.Exists(Path.Combine(chapterDir, ExplorerFile.ReadedMarkerFileName))) + 1;
            for (int i = Math.Max(0, firstUnreadIndex - 3); i < chapterDirs.Count; ++i)
            {
                var chapterDir = chapterDirs[i];

                var pages = Directory.GetFiles(chapterDir)
                    .Where(path => Path.GetExtension(path).IsImage())
                    .OrderBy(Path.GetFileName)
                    .Select(Path.GetFullPath)
                    .ToList();
                if (pages.Count > 0)
                    result.Add(new ExplorerFile(mangaName, Path.GetFullPath(chapterDir), pages, i == firstUnreadIndex));
            }
            return Task.FromResult(result);
        }

        public override Task SetReaded(ExplorerFileBase file)
        {
            var chapter = (ExplorerFile)file;
            if (file.readed)
                File.WriteAllText(chapter.ReadedMarkerPath, DateTime.Now.ToString("O"));
            else if (File.Exists(chapter.ReadedMarkerPath))
                File.Delete(chapter.ReadedMarkerPath);
            return Task.CompletedTask;
        }
    }
}
