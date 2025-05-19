using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace PictureSpider
{
    public class Downloader
    {
        public enum DownloaderPostfix//为了不同的DownloadQueue不互相干扰，把aria2c.exe复制多份，Downloader表示使用哪个exe
        {
            Twitter = 0,
            Pixiv = 1,
            Hitomi = 2,
            Telegram = 3,
            Kemono = 4,
            Hentaivox = 5,
        }
        public enum DownloaderType
        {
            Aria2DownloadQueue,
            MegaDownloadQueue,
        }
        public List<BaseDownloadQueue> downloaders=new List<BaseDownloadQueue>();
        public Downloader(params BaseDownloadQueue[] _downloaders)
        {
            downloaders= _downloaders.ToList();
        }
        public Downloader(List<BaseDownloadQueue> _downloaders)
        {
            downloaders = _downloaders;
        }
        public Downloader(BaseDownloadQueue _downloaders)
        {
            downloaders.Add(_downloaders);
        }
        public async Task<bool> Add(DownloaderType downloaderType, string url, string _dir, string _filename)
        {
            var downloader = GetDownloader(downloaderType);
            if (downloader == null)
                return false;
            var path = Path.Combine(_dir, _filename);//防止filename中有/
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return await downloader.Add(url,dir, Path.GetFileName(path));
        }
        public async Task<bool> Add(BaseWork work,string root_dir)
        {
            var path = Path.GetFullPath(Path.Combine(root_dir, work.TmpSubPath));
            return await Add(work.GetDownloader, work.DownloadURL,Path.GetDirectoryName(path) ,Path.GetFileName(path));
        }

        public BaseDownloadQueue GetDownloader(DownloaderType downloaderType)
        {
            return downloaders.Where(downloader => downloader.GetType().Name == downloaderType.ToString()).FirstOrDefault();
        }
        public async Task WaitForAll()
        {
            foreach (var downloader in downloaders)
                await downloader.WaitForAll();
        }
    }
}
