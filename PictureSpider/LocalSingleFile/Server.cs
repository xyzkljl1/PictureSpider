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
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;

namespace PictureSpider.LocalSingleFile
{
    public partial class Server : BaseServerWithDB<Database>, IDisposable
    {
        private sealed class NaturalStringComparer : IComparer<string>
        {
            public static readonly NaturalStringComparer Instance = new NaturalStringComparer();

            [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
            private static extern int StrCmpLogicalW(string x, string y);

            public int Compare(string x, string y)
            {
                return StrCmpLogicalW(x, y);
            }
        }

        private string FavDir;
        private List<string> TmpDirs;
        public static HashSet<string> valid_exts = new HashSet<string>{".jpg",".png",".webp",".gif", ".jpeg" };
        public Server(Config config):base(config, config.LSFConnectStr)
        {
            logPrefix = "L";
            FavDir = config.LSFFavDir;
            TmpDirs = config.LSFTmpDirs;
            if(!Directory.Exists(FavDir))
                Directory.CreateDirectory(FavDir);
        }
        public void Dispose()
        {
        }
#pragma warning disable CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
#pragma warning disable CS0162 // 检测到无法访问的代码
        public override async Task Init()
        {
#if DEBUG
            return;
#endif
            _ = RunSchedule();
        }
#pragma warning restore CS0162
#pragma warning restore CS1998
        private async Task RunSchedule()
        {
            do
            {
                //await ReloadDb();
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
                    var dest_path = Path.Combine(FavDir, illust.sub_path);
                    var dest_dir= Path.GetDirectoryName(dest_path);
                    while (File.Exists(dest_path))
                    {//如果目的地存在同名文件就一直加_直到不重名
                        filename = $"_{filename}";
                        dest_path=Path.Combine(dest_dir,filename);
                    }
                    if(!Directory.Exists(dest_dir))
                        Directory.CreateDirectory(dest_dir);
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
            foreach (var dir in TmpDirs)
                Util.ClearEmptyFolders(dir, DateTime.UtcNow.AddMonths(-1));
        }
#pragma warning disable CS1998 // 此异步方法缺少 "await" 运算符，将以同步方式运行
        public async override Task<List<ExplorerQueue>> GetExplorerQueues()
#pragma warning restore CS1998
        {
            var ret = new List<ExplorerQueue>();
            ret.Add(new ExplorerQueue(ExplorerQueue.QueueType.Fav, "0", "LSF-Fav"));
            foreach (var dir in TmpDirs)
                ret.Add(new ExplorerQueue(ExplorerQueue.QueueType.Folder, dir,$"LSF-{Path.GetFileName(dir)}"));
            return ret;
        }
#pragma warning disable CS1998 // 此异步方法缺少 "await" 运算符，将以同步方式运行
        public async override Task<List<ExplorerFileBase>> GetExplorerQueueItems(ExplorerQueue queue)
#pragma warning restore CS1998
        {
            var result = new List<ExplorerFileBase>();
            if (queue.type == ExplorerQueue.QueueType.Fav)
            {
                result.AddRange(GetFavoriteFiles());
                if (Directory.Exists(download_dir_unified_fav))
                    foreach (var dir in Directory.GetDirectories(download_dir_unified_fav)
                        .OrderBy(x => Path.GetFileName(x), NaturalStringComparer.Instance))
                        result.AddRange(GetFavoriteFiles(Path.GetFileName(dir)));
            }
            else if (queue.type == ExplorerQueue.QueueType.User)
                result.AddRange(GetFavoriteFiles(queue.id));
            else if (queue.type == ExplorerQueue.QueueType.Folder)
            {
                var readed=database.Waited.Select(x => x.path).ToHashSet<String>();
                var dir = queue.id;
                var existedFiles = GetFiles(dir);
                foreach (var group in existedFiles.Where(x => !readed.Contains(x))
                    .GroupBy(x => Path.GetDirectoryName(x), StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => Path.GetRelativePath(dir, x.Key), NaturalStringComparer.Instance))
                    foreach (var paths in group.OrderBy(x => Path.GetFileName(x), NaturalStringComparer.Instance)
                        .Select(x => Path.GetFullPath(x)).Chunk(500))
                        result.Add(new ExplorerFile(paths,dir, false));
            }
            return result;
        }
        private List<ExplorerFileBase> GetFavoriteFiles(string storageName = null)
        {
            var dir = storageName == null ? FavDir : Path.Combine(download_dir_unified_fav, storageName, "lsf");
            var root = storageName == null ? FavDir : download_dir_unified_fav;
            var result = new List<ExplorerFileBase>();
            foreach (var group in GetFiles(dir).GroupBy(x => Path.GetDirectoryName(x), StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => Path.GetRelativePath(root, x.Key), NaturalStringComparer.Instance))
                foreach (var paths in group.OrderBy(x => Path.GetFileName(x), NaturalStringComparer.Instance)
                    .Select(x => Path.GetFullPath(x)).Chunk(500))
                    result.Add(new ExplorerFile(paths, root, true));
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
        public override Task SetReaded(ExplorerFileBase file)
        {
            var group = (ExplorerFile)file;
            for (int page = 0; page < group.paths.Count; page++)
            {
                if (group.pageBookmarks[page])
                    continue;
                var path = group.paths[page];
                var pathraw = Util.String2Bytes(path);
                var exists = (from illust in database.Waited
                              where illust.path_raw == pathraw
                              select illust).ToList<Illust>();
                if (file.readed)
                {
                    if (exists.Count == 0)
                        database.Waited.Add(new Illust(path, Path.GetRelativePath(group.root_path, path), false));
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
            }
            database.SaveChanges();
            return Task.CompletedTask;
        }
        public override Task SwitchBookmark(ExplorerFileBase file, int page)
        {
            var group = (ExplorerFile)file;
            var path = group.paths[page];
            var bookmarked = !group.pageBookmarks[page];
            var pathraw = Util.String2Bytes(path);
            var exists = (from illust in database.Waited
                          where illust.path_raw == pathraw
                          select illust).ToList<Illust>();
            if (bookmarked)
            {
                if (exists.Count == 0)
                    database.Waited.Add(new Illust(path, Path.GetRelativePath(group.root_path, path), true));
                else//默认只有一个
                {
                    if(group.in_fav_dir)//原本在fav目录则无需操作，从队列里移除
                        database.Waited.Remove(exists.First());
                    else//原本不在则需要留在队列
                    {
                        exists.First().fav = true;
                        exists.First().date = DateTime.Now;
                    }
                }
            }
            else
            {
                //被取消bookmark的图片一定是已读的，仍然需要删除
                if (exists.Count == 0)
                    database.Waited.Add(new Illust(path, Path.GetRelativePath(group.root_path, path), false));
                else//默认只有一个
                {
                    exists.First().fav = false;
                    exists.First().date = DateTime.Now;
                }
            }
            database.SaveChanges();
            group.pageBookmarks[page] = bookmarked;
            //全部收藏时才跳过整组已读处理，部分收藏时仍需处理其余图片。
            group.bookmarked = group.pageBookmarks.All(x => x);
            return Task.CompletedTask;
        }

    }
}
