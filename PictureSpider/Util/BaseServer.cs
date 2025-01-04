using Fizzler;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PictureSpider
{
    public abstract class BaseServer
    {
        public bool tripleBookmarkState = false;//如果为否则bookmark只有是否两种状态，bookmarkPrivate无效
        public string logPrefix = "";
        //Init应当在构建之后，使用之前，在主线程(UI线程)中调用并等待完成
        public virtual Task Init() { return Task.CompletedTask; }
        //获得所有浏览队列的列表
#pragma warning disable CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
        public async virtual Task<List<ExplorerQueue>> GetExplorerQueues() { return new List<ExplorerQueue>();}
                              //获得一个队列中的所有文件
        public async virtual Task<List<ExplorerFileBase>> GetExplorerQueueItems(ExplorerQueue queue) { return new List<ExplorerFileBase>(); }
#pragma warning restore CS1998
        //默认切到下一组
        public virtual void ExplorerQueueSwitchVertical(List<ExplorerFileBase> file_list,bool manually,int offset,
                                                        int cur_index,int cur_sub_index,bool to_end ,
                                                        out int new_index,out int new_sub_index)
        {
            new_index = -1;
            new_sub_index = -1;
            if (Math.Abs(offset) > 1) throw new NotImplementedException("");
            new_index = cur_index + offset;
            if (new_index >= 0 && new_index < file_list.Count)
            {
                if (to_end)//找到最前/最后一个valid的page
                {
                    for (new_sub_index = file_list[new_index].pageCount() - 1;
                            new_sub_index >= 0 && new_sub_index < file_list[new_index].pageCount();
                            new_sub_index--)
                        if (file_list[new_index].isPageValid(new_sub_index))
                            return;
                }
                else
                {
                    for (new_sub_index = 0;
                            new_sub_index >= 0 && new_sub_index < file_list[new_index].pageCount();
                            new_sub_index++)
                        if (file_list[new_index].isPageValid(new_sub_index))
                            return;
                }
            }
            new_index = -1;
            new_sub_index = -1;
        }
        public virtual void SetReaded(ExplorerFileBase file) { }
        public virtual void SetBookmarked(ExplorerFileBase file) { }
        public virtual void SetBookmarkEach(ExplorerFileBase file) { }
        public virtual BaseUser GetUserById(string id) { return null; }
        public virtual void SetUserFollowOrQueue(BaseUser user) { }
        public virtual Dictionary<string, TagStatus> GetAllTagsStatus() { return new Dictionary<string, TagStatus>(); }
        public virtual Dictionary<string, string> GetAllTagsDesc() { return new Dictionary<string, string>(); }
        public virtual void UpdateTagStatus(string tag,TagStatus status) { }
        public virtual void Log(string text)
        {
            Console.WriteLine($"{logPrefix} {DateTime.Now.ToString("MM/dd-HH:mm:ss")} {text}");
        }
        public virtual void LogError(string text)
        {
            Console.Error.WriteLine($"ERROR {logPrefix} {DateTime.Now.ToString("MM/dd-HH:mm:ss")} {text}");
        }
        //以下是给listenerServer调用的部分
        public virtual bool ListenerUtil_IsValidUrl(string url) { return false; }
#pragma warning disable CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
        public async virtual Task<bool> ListenerUtil_FollowUser(string url) { return false; }
        public virtual async Task ListenerUtil_SetCookie(string cookie) { }
#pragma warning restore CS1998

        //File操作，因为需要Log，放到baseserver里
        public int DeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Log($"Fail to delete {path}:{ex.Message}");
            }
            return 0;
        }
        public int CopyFile(string src,string dest)
        {
            try
            {
                if (File.Exists(src))
                {
                    if(!Directory.Exists(Path.GetDirectoryName(dest)))
                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    File.Copy(src, dest, true);
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Log($"Fail to copy {src} to {dest}:{ex.Message}");
            }
            return 0;
        }
    }
}
