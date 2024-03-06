using System;
using System.Collections.Generic;
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
        public async virtual Task<bool> ListenerUtil_FollowUser(string url) { return false; }
        public virtual async Task ListenerUtil_SetCookie(string cookie) { }
    }
}
