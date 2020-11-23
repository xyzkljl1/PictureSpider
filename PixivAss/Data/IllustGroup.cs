using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace PixivAss.Data
{
    public enum TagStatus
    {
        None,
        Follow,
        Ignore
    };
    class User
    {
        //Original Data
        public int userId;
        public string userName;
        public Boolean followed;
        public Boolean queued;
        public User(int _id,string _name,Boolean _f, Boolean _q)
        {
            userId = _id;
            userName = _name;
            followed = _f;
            queued = _q;
        }
        public User(JObject json)
        {
            userId = json.Value<int>("userId");
            userName = json.Value<string>("userName");
            followed = json.Value<Boolean>("following");
            queued = false;
        }
    }
    class Illust
    {
        //Original Data
        public int id;//same as illustId
        public string title;//=illustTitle
        public string description;// same as illustComment
        public int xRestrict;//is not public
        public List<string> tags;//name
        public int userId;
        public int width;
        public int height;
        public int pageCount;
        public Boolean bookmarked;//整体
        public Boolean bookmarkPrivate;
        public int likeCount = 0;
        public int bookmarkCount=0;
        public bool valid;//不在json里,获取不到(即已删除)则为无效
        //Modified data
        public string urlFormat;//Lets assume url of each page has same format as p0 and they never change
        public string urlThumbFormat;
        //My Data
        public Boolean readed;
        public string bookmarkEach;//分别,0:no,1:Pub,2:Private
        public DateTime updateTime;
        //tmp
        public string userName;
        public int score;
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
        public Illust(JObject json)
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
            likeCount = json.Value<int>("likeCount");
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
        }
    }
}
