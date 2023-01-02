using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PictureSpider.Twitter
{
    public enum MediaType
    {
        Image =1,
        Video=2,
        Other=3
    }

    /*
     * 属性分为Base和Extra部分，Base是可以从远端直接/间接获取的属性，Extra是由我赋予的属性
     */
    // User在数据库中以name为主键，id为unique index，因为手动添加user时通常只知道name
    internal class User:BaseUser
    {
        //Base
        //为了使用Dapper定义成属性，下同
        public string id { get; set; }//数字id
        public string name { get; set; }//唯一名字，接在@后面的部分，不包括@
        public string nick_name { get; set; }//昵称
        //此ID以前(小于)的tweet都已经完全获取过
        //因为search返回的推不全，而api返回的有时限，所以latest_tweet_id需要分别计
        public string search_latest_tweet_id { get; set; }
        public string api_latest_tweet_id { get; set; }
        public User()
        {

        }
        public User(string _id, string _name, string _nick_name, bool _followed=false, bool _queued=false)
        {
            base.displayId = id = _id;
            base.displayText = name = _name;
            nick_name = _nick_name;
            base.followed = _followed;
            base.queued = _queued;
        }
        public void InitDisplayText()
        {
            base.displayId = id;
            base.displayText = name;
        }
    }
    internal class Tweet
    {
        //Base
        public string id { get; set; }
        public DateTime created_at { get; set; }
        public string full_text { get; set; }
        public string user_id { get; set; }
        public string url { get; set; }
    }
    public class Media
    {
        //Base
        public string id { get; set; }//同key，搜索时可以获得一个id(和key不同，形如1604559279322968065)，但是从api查询时只会获得key，再去获得id没有意义，暂时使用key替代id
        public string key { get; set; }//形如3_1605831705872388098，apiv2查询attachments时返回的key
        public string user_id { get; set; }//user_id,冗余,加速用
        public string tweet_id { get; set; }
        public string url { get; set; }//源文件下载地址
        public string expand_url { get; set; }//在网页上展开该media的地址
        public MediaType media_type { get; set; }
        public string file_name { get; set; }
        //Extra
        public Boolean downloaded { get; set; } = false;
        public Boolean readed { get; set; } = false;
        public Boolean bookmarked { get; set; } = false;

    }
    public class ExplorerFile : ExplorerFileBase
    {
        //基类中定义的属性在基类中修改，未定义的在illust中
        public Media media;
        public string download_dir_main;
        public ExplorerFile(Media _illust, string _download_dir)
        {
            media = _illust;
            download_dir_main = _download_dir;
            title = "";
            description = "";
            id = media.id.ToString();
            tags = new List<string>();
            userId = media.user_id;
            bookmarked = media.bookmarked;
            bookmarkPrivate = false;
            readed = media.readed;
        }
        public override string FilePath(int page)
        {
            return Path.Combine(download_dir_main, media.file_name);
        }

        public override int pageCount() { return 1; }

        public override string WebsiteURL(int page) { return media.expand_url; }

        public override int validPageCount() { return 1; }
    }
}
