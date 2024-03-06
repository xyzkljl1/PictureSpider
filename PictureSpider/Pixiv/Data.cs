using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace PictureSpider.Pixiv
{
    public class User : BaseUser
    {
        //Original Data
        public int userId;
        public string userName;
        public User(int _id,string _name,Boolean _f, Boolean _q)
        {
            userId = _id;
            userName = _name;
            base.displayId = userId.ToString();
            base.displayText=userName;
            base.followed = _f;
            base.queued = _q;
        }
        public User(JObject json)
        {
            userId = json.Value<int>("userId");
            userName = json.Value<string>("userName");
            base.displayId = userId.ToString();
            base.displayText = userName;
            base.followed = json.Value<Boolean>("following");
            base.queued = false;
        }
    }
    public class Illust
    {
        //Original Data
        public int id;//same as illustId
        public string title;//=illustTitle
        public string description;// same as illustComment
        public int xRestrict;//is not public
        public List<string> tags;//name
        public int userId=0;
        public int width;
        public int height;
        public int pageCount;
        public Boolean bookmarked;//整体
        public Boolean bookmarkPrivate;
        public int likeCount = 0;
        public int bookmarkCount = 0;
        public int viewCount = 0;
        public bool valid;//不在json里,获取不到(即已删除)则为无效

        public DateTime uploadDate;//illust上传的时间 
        //Modified data
        public string urlFormat;//假定每P的格式都相同
        public string urlThumbFormat;
        public string ugoiraFrames="";//动图帧，json格式，假定动图全部只有1p
        public string ugoiraURL="";//动图url,如果此项不为空则为动图
        //My Data
        public Boolean readed;
        public string bookmarkEach="";/*为空表示全部有效；不为空且长度等于page时，为1的位表示忽略。
                                        string浪费空间且修改消耗大，但是考虑到读远比写次数多，且多数illust的bookmarkEach为空，直接使用string以方便数据库交互
                                        */
        public DateTime updateTime;//数据库中该条record更新的时间(不是illust更新时间)
        //tmp
        public string userName;
        public int score;
        public string debugMsg;
        public Illust(int _id,bool _valid)
        {
            id = _id;
            valid = _valid;
        }
        public Illust(int _id,string _urlFormat,int _pageCount)
        {
            urlFormat = _urlFormat;
            id = _id;
            pageCount = _pageCount;
            valid = true;
        }
        public Illust(JObject json,JObject ugoira_json=null)
        {
            valid = true;
            id = json.Value<int>("illustId");
            title = json.Value<string>("illustTitle");
            description = json.Value<string>("illustComment");
            if (description.Length > 6000)
                description = description.Substring(0, 5999);
            xRestrict = json.Value<int>("xRestrict");
            this.tags = new List<string>();
            if (json.Value<JObject>("tags").Value<JArray>("tags") != null)
                foreach (var tag in json.Value<JObject>("tags").Value<JArray>("tags"))
                    this.tags.Add(tag.ToObject<JObject>().Value<string>("tag"));
            userId = json.Value<int>("userId");
            userName = json.Value<string>("userName");
            width = json.Value<int>("width");
            height = json.Value<int>("height");
            pageCount = json.Value<int>("pageCount");
            bookmarkCount = json.Value<int>("bookmarkCount");
            viewCount = json.Value<int>("viewCount");
            likeCount = json.Value<int>("likeCount");
            uploadDate = json.Value<DateTime>("uploadDate");//形如2019-05-30T08:08:00+00:00
            if (json.Value<JObject>("bookmarkData") != null)
            {
                bookmarked = true;
                bookmarkPrivate = json.Value<JObject>("bookmarkData").Value<Boolean>("private");
            }
            else
                bookmarked = bookmarkPrivate = false;
            {
                urlFormat = json.Value<JObject>("urls").Value<string>("original");
                int idx = urlFormat.LastIndexOf("ugoira0");
                if (idx>=0)
                    urlFormat = urlFormat.Insert(idx+6, "{").Insert(idx + 8, "}");
                else
                {
                    idx = urlFormat.LastIndexOf("p0")+1;
                    if (idx>= 0)
                        urlFormat = urlFormat.Insert(idx, "{").Insert(idx + 2, "}");
                }
            }
            {
                urlThumbFormat = json.Value<JObject>("urls").Value<string>("regular");
                int idx = urlThumbFormat.LastIndexOf("p0") + 1;
                if(idx>=1)
                    urlThumbFormat = urlThumbFormat.Insert(idx, "{").Insert(idx + 2, "}");
            }
            updateTime = DateTime.UtcNow;
            readed = false;
            bookmarkEach = "";
            if (ugoira_json!=null)
            {
                ugoiraURL = ugoira_json.Value<string>("originalSrc");
                ugoiraFrames = "";
                foreach (var frame in ugoira_json.Value<JArray>("frames"))
                    ugoiraFrames +=frame.Value<String>("file")+"`"+frame.Value<String>("delay")+"`";
            }
        }
        public bool isPageValid(int page)
        {
            return bookmarkEach.Count()==pageCount? bookmarkEach[page] == '0':true;
        }
        public void switchPageValid(int page)
        {
            if(bookmarkEach.Count()!=pageCount)//长度不一致时旧的作废
                bookmarkEach =new String('0', pageCount);
            if (pageCount < 2)//只有一页的没有单标的意义
                return;
            if (bookmarkEach[page]=='0')
                bookmarkEach = bookmarkEach.Remove(page, 1).Insert(page, "1");
            else
                bookmarkEach = bookmarkEach.Remove(page, 1).Insert(page, "0");
        }
        public int validPageCount()
        {
            if (bookmarkEach.Count() == pageCount)
                return bookmarkEach.Sum(x=>x=='0'?1:0);
            return pageCount;
        }
        public bool isUgoira()
        {
            return ugoiraURL.Length > 0;
        }
        public string URL(int page)
        {
            if (isUgoira())
                return ugoiraURL;//假定动图只有一p
            return String.Format(urlFormat, page);
        }
        public string downloadFileName(int page)//下载的文件名
        {
            string ext = "";
            string url = URL(0);
            int pos = url.LastIndexOf(".");
            if (pos >= 0)
                ext = url.Substring(pos + 1);
            return String.Format("{0}_p{1}.{2}", id, page, ext);
        }
        public string storeFileName(int page)//本地存储的文件名
        {
            if (isUgoira())
                return String.Format("{0}_p{1}.gif", id, page);
            return downloadFileName(page);
        }
        public bool shouldDownload(int page)
        {
            if (!valid)
                return false;
            if (bookmarked)
                return isPageValid(page);
            if (readed)
                return false;
            return true;
        }
    }
    public class ExplorerFile : ExplorerFileBase
    {
        //基类中定义的属性在基类中修改，未定义的在illust中
        public Illust illust;
        public string download_dir_main;
        public ExplorerFile(Illust _illust, string _download_dir)
        {
            illust = _illust;
            download_dir_main = _download_dir;
            title = illust.title;
            description = illust.description;
            id=illust.id.ToString();
            tags=illust.tags;
            userId=illust.userId.ToString();
            bookmarked=illust.bookmarked;
            bookmarkPrivate =illust.bookmarkPrivate;
            readed=illust.readed;
            if (!illust.valid)
                debugMessage = "Invalid!</br>";
            debugMessage += illust.debugMsg;
        }
        public override string FilePath(int page)
        {
            return Path.Combine(download_dir_main, illust.storeFileName(page));
        }

        public override int pageCount() { return illust.pageCount;}

        public override string WebsiteURL(int page) { return $"https://www.pixiv.net/artworks/{illust.id}"; }

        public override int validPageCount() { return illust.validPageCount();}

        public override bool isPageValid(int page) { return illust.isPageValid(page); }
        public override void switchPageValid(int page) { illust.switchPageValid(page); }
    }
}
