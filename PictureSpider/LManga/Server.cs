using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PictureSpider.LManga
{
    public class Server : BaseServer, IDisposable
    {
        private readonly List<string> rootDirs;

        public Server(Config config)
        {
            logPrefix = "LM";
            rootDirs = config.LMangaRootDirs;
            foreach (var dir in rootDirs)
                Util.TouchDir(dir);
        }

        public void Dispose()
        {
        }

        public override Task<List<ExplorerQueue>> GetExplorerQueues()
        {
            var result = new List<ExplorerQueue>();
            foreach (var rootDir in rootDirs.Where(Directory.Exists))
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
            foreach (var chapterDir in Directory.GetDirectories(queue.id).OrderBy(Path.GetFileName).Reverse())
            {
                if (File.Exists(Path.Combine(chapterDir, ExplorerFile.ReadedMarkerFileName)))
                    break;

                var pages = Directory.GetFiles(chapterDir)
                    .Where(path => Path.GetExtension(path).IsImage())
                    .OrderBy(Path.GetFileName)
                    .Select(Path.GetFullPath)
                    .ToList();
                if (pages.Count > 0)
                    result.Add(new ExplorerFile(mangaName, Path.GetFullPath(chapterDir), pages));
            }
            result.Reverse();
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
