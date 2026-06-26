using System.Collections.Generic;
using System.IO;

namespace PictureSpider.LManga
{
    public class ExplorerFile : ExplorerFileBase
    {
        public const string ReadedMarkerFileName = ".lmanga.readed";
        public string chapterDir;
        public List<string> pages;

        public ExplorerFile(string mangaName, string _chapterDir, List<string> _pages)
        {
            chapterDir = _chapterDir;
            pages = _pages;
            var chapterName = Path.GetFileName(chapterDir);
            id = chapterDir;
            title = $"{mangaName} - {chapterName}";
            description = $"{chapterName}<br>{pages.Count} pages";
            userId = mangaName;
            readed = File.Exists(ReadedMarkerPath);
        }

        public string ReadedMarkerPath => Path.Combine(chapterDir, ReadedMarkerFileName);
        public override string FilePath(int page) { return pages[page]; }
        public override int pageCount() { return pages.Count; }
        public override int validPageCount() { return pages.Count; }
        public override string WebsiteURL(int page) { return ""; }
    }
}
