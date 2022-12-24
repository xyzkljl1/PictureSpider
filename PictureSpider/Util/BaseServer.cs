using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PictureSpider
{
    public abstract class BaseServer
    {
        //Init应当在构建之后，使用之前，在主线程(UI线程)中调用并等待完成
        public abstract Task Init();
        //获得所有浏览队列的列表
        public abstract Task<List<ExplorerQueue>> GetExplorerQueues();
        //获得一个队列中的所有文件
        public abstract Task<List<ExplorerFileBase>> GetExplorerQueueItems(ExplorerQueue queue);
        public abstract void SetReaded(ExplorerFileBase file);
        public abstract void SetBookmarked(ExplorerFileBase file);
        public abstract void SetBookmarkEach(ExplorerFileBase file);
    }
}
