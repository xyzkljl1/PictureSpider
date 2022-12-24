using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    internal class User
    {
        //Base
        //为了使用Dapper定义成属性，下同
        public string id { get; set; }//数字id
        public string name { get; set; }//唯一名字，接在@后面的部分，不包括@
        public string nick_name { get; set; }//昵称
        public Boolean followed { get; set; }
        public Boolean queued { get; set; }
        public string latest_tweet_id { get; set; }//此ID以前(小于)的tweet都已经完全获取过
        public User()
        {

        }
        public User(string _id, string _name, string _nick_name, bool _followed=false, bool _queued=false)
        {
            id = _id;
            name = _name;
            nick_name = _nick_name;
            followed = _followed;
            queued = _queued;
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
    internal class Media
    {
        //Base
        public string id { get; set; }//形如1604559279322968065
        public string key { get; set; }//形如3_1605831705872388098，apiv2查询attachments时返回的key
        public string user_id { get; set; }//user_id,冗余,加速用
        public string tweet_id { get; set; }
        public string url { get; set; }
        public MediaType media_type { get; set; }
        public string file_name { get; set; }
        //Extra
        public Boolean fav { get; set; } = false;
        public Boolean downloaded { get; set; } = false;
    }
}
