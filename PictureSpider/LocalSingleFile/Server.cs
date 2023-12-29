using Microsoft.AspNetCore.WebUtilities;
using Microsoft.ClearScript.V8;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using PictureSpider;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;

namespace PictureSpider.LocalSingleFile
{
    public partial class Server : BaseServer, IDisposable
    {
        private Database database;
        private string FavDir;
        private List<string> TmpDirs;
        public static HashSet<string> valid_exts = new HashSet<string>{".jpg",".png",".webp",".gif" };
        public Server(Config config)
        {
            logPrefix = "L";
            database = new Database(config.LSFConnectStr);
            FavDir = config.LSFFavDir;
            TmpDirs = config.LSFTmpDirs;
            if(!Directory.Exists(FavDir))
                Directory.CreateDirectory(FavDir);
        }
        public void Dispose()
        {
            database.Dispose();
        }
        public override async Task Init()
        {
#if DEBUG
            /return;
#endif
#pragma warning disable CS0162 // 检测到无法访问的代码
#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
            RunSchedule();
#pragma warning restore CS4014
#pragma warning restore CS0162
        }
        private async Task RunSchedule()
        {
            do
            {
                ProcessWaited();
                await Task.Delay(new TimeSpan(24*7, 0, 0));
            }
            while (true);
        }
        //处理标记为bookmark/readed但未处理的文件
        public void ProcessWaited()
        {
            var illusts = (from illust in database.Waited
                          select illust).ToList<Illust>();
            int del_ct = 0;
            int mv_ct = 0;
            foreach(var illust in illusts)
            {
                if (!File.Exists(illust.path))
                    continue;
                if(illust.fav)
                {
                    var filename = Path.GetFileName(illust.path);
                    //用目录的hash作前缀加原文件名命名，从而使本来具有一定关联的图片，在移动到fav目录后仍然能排在一起
                    var prefix = $"{Path.GetDirectoryName(illust.path).GetHashCode()}".Substring(0,3);
                    var dest_path = "";
                    do
                    {//如果目的地存在同名文件就一直加_直到不重名
                        filename = $"_{filename}";
                        dest_path=Path.Combine(FavDir, $"{prefix}{filename}");
                    }while(File.Exists(dest_path));
                    try
                    {
                        File.Move(illust.path, dest_path);
                        database.Waited.Remove(illust);
                        database.SaveChanges();
                        mv_ct++;
                    }
                    catch (System.IO.IOException)
                    {
                        //说明文件被占用，忽略
                    }
                }
                else
                {
                    try
                    {
                        File.Delete(illust.path);
                        database.Waited.Remove(illust);
                        database.SaveChanges();
                        del_ct++;
                    }
                    catch (System.IO.IOException)
                    {
                        //说明文件被占用，忽略
                    }
                }
            }
            Log($"Weekly Task Done:Move {mv_ct}/Del {del_ct}.");
        }
        public async override Task<List<ExplorerQueue>> GetExplorerQueues()
        {
            var ret = new List<ExplorerQueue>();
            ret.Add(new ExplorerQueue(ExplorerQueue.QueueType.Fav, "0", "LSF-Fav"));
            foreach (var dir in TmpDirs)
                ret.Add(new ExplorerQueue(ExplorerQueue.QueueType.Folder, dir,$"LSF-{Path.GetFileName(dir)}"));
            return ret;
        }
        public async override Task<List<ExplorerFileBase>> GetExplorerQueueItems(ExplorerQueue queue)
        {
            var result = new List<ExplorerFileBase>();
            if (queue.type == ExplorerQueue.QueueType.Fav)
            {
                var existedFiles = GetFiles(FavDir);
                foreach (var path in existedFiles)
                    result.Add(new ExplorerFile(Path.GetFullPath(path), true));
            }
            else if (queue.type == ExplorerQueue.QueueType.Folder)
            {
                var readed=database.Waited.Select(x => x.path).ToHashSet<String>();
                var dir = queue.id;
                var existedFiles = GetFiles(dir);
                foreach (var path in existedFiles)
                    if(!readed.Contains(path))
                        result.Add(new ExplorerFile(Path.GetFullPath(path), false));
            }
            return result;
        }
        //获取该目录及子目录下所有图片文件,为便于比较统一使用Path.GetFullPath
        HashSet<string> GetFiles(string dir)
        {
            var result=new HashSet<string>();
            if(!Directory.Exists(dir))
                return result;
            foreach (var path in Directory.GetFiles(dir))
            {
                var ext=Path.GetExtension(path).ToLower();
                if(valid_exts.Contains(ext))
                    result.Add(path);
            }
            foreach (var path in Directory.GetDirectories(dir))
                result=result.Union(GetFiles(path)).ToHashSet<string>();
            return result;
        }
        public override BaseUser GetUserById(string id)
        {
            return null;
        }
        public override void SetReaded(ExplorerFileBase file)
        {
            var path= (file as ExplorerFile).path;
            var exists = (from illust in database.Waited
                          where illust.path == path
                          select illust).ToList<Illust>();
            if (file.readed)
            {
                if (exists.Count == 0)
                    database.Waited.Add(new Illust((file as ExplorerFile).path, false));
                else 
                {
                    exists.First().readed = true;//默认只有一个
                    exists.First().date = DateTime.Now;
                }
            }
            else
            {
                if (exists.Count == 0)
                {
                    //Do Nothing，按理说不会出现此种情况
                    throw new Exception("Why?");
                }
                else//应当直接从数据库里删除，因为数据库只存待删除/待移动的文件
                    foreach (var illust in exists)
                        database.Waited.Remove(illust);
            }
            database.SaveChanges();
        }
        public override void SetBookmarked(ExplorerFileBase file)
        {
            var path = (file as ExplorerFile).path;
            var exists = (from illust in database.Waited
                          where illust.path == path
                          select illust).ToList<Illust>();
            if (file.bookmarked)
            {
                if (exists.Count == 0)
                    database.Waited.Add(new Illust((file as ExplorerFile).path, true));
                else//默认只有一个
                {
                    exists.First().fav = true;
                    exists.First().date = DateTime.Now;
                }
            }
            else
            {
                //被取消bookmark的图片一定是已读的，仍然需要删除
                if (exists.Count == 0)
                    database.Waited.Add(new Illust((file as ExplorerFile).path, false));
                else//默认只有一个
                {
                    exists.First().fav = false;
                    exists.First().date = DateTime.Now;
                }
            }
            database.SaveChanges();
        }
    }
}