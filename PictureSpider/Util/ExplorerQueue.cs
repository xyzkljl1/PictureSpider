using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PictureSpider
{
    public abstract class ExplorerFileBase
    {
        public string id;
        public string title;
        public string description="";
        public string userId;
        public bool bookmarked;
        public bool bookmarkPrivate;
        public List<string> tags=new List<string>();
        public bool readed;
        public string debugMessage = "";
        public abstract int pageCount();
        public abstract int validPageCount();
        public abstract string WebsiteURL(int page);
        public abstract string FilePath(int page);
        public virtual bool isPageValid(int page) { return true; }
        public virtual void switchPageValid(int page) { }
    }
    public struct ExplorerQueue
    {
        public enum QueueType
        {
            Fav,
            FavR,
            Main,
            MainR,
            User,
            Folder
        };
        public QueueType type;//队列类型
        public string id;//区分队列的id,在不同Server中意义不同
        public string displayText;//显示在UI上的队列名
        public ExplorerQueue(QueueType _type, string _id, string _text)
        {
            type = _type;
            id = _id;
            displayText = _text;
        }
    }

}

