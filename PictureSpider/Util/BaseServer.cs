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
        public abstract Task Init();
        //获得所有浏览队列的列表
        public abstract Task<List<ExplorerQueue>> GetExplorerQueues();
        //获得一个队列中的所有文件
        public abstract Task<List<ExplorerFileBase>> GetExplorerQueueItems(ExplorerQueue queue);
        public abstract void SetReaded(ExplorerFileBase file);
        public abstract void SetBookmarked(ExplorerFileBase file);
        public virtual void SetBookmarkEach(ExplorerFileBase file) { }
        public abstract BaseUser GetUserById(string id);
        public abstract void SetUserFollowOrQueue(BaseUser user);
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

    }
}
