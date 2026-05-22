using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;

namespace PictureSpider.Twitter
{
    public enum MediaType
    {
        Image = 1,
        Video = 2,
        Other = 3
    }

    /*
     * 属性分为Base和Extra部分，Base是可以从远端直接/间接获取的属性，Extra是由我赋予的属性
     */
    [Table("user")]
    public class User : BaseUser
    {
        [Key]
        [MaxLength(64)]
        public virtual string id { get; set; } = "";//数字id
        [Required]
        [MaxLength(128)]
        public virtual string name { get; set; } = "";//唯一名字，接在@后面的部分，不包括@
        [Required]
        [MaxLength(300)]
        public virtual string nick_name { get; set; } = "";//昵称
        // 此ID以前(小于)的tweet都已经完全获取过。当前 Web GraphQL 流程继续复用历史字段名。
        [Required]
        [MaxLength(64)]
        public virtual string search_latest_tweet_id { get; set; } = "0";
        [Required]
        [MaxLength(64)]
        public virtual string api_latest_tweet_id { get; set; } = "0";
        public virtual bool invalid { get; set; } = false;

        public User() { }

        public User(string _id, string _name, string _nick_name, bool _followed = false, bool _queued = false)
        {
            displayId = id = _id;
            displayText = name = _name;
            nick_name = _nick_name;
            search_latest_tweet_id = "0";
            api_latest_tweet_id = "0";
            followed = _followed;
            queued = _queued;
        }

        public void InitDisplayText()
        {
            displayId = id;
            displayText = name;
        }
    }

    [Table("tweet")]
    public class Tweet
    {
        [Key]
        [MaxLength(64)]
        public virtual string id { get; set; } = "";
        public virtual DateTime created_at { get; set; }
        [Column(TypeName = "text")]
        public virtual string full_text { get; set; } = "";
        [Required]
        [MaxLength(64)]
        public virtual string user_id { get; set; } = "";
        [Required]
        [MaxLength(500)]
        public virtual string url { get; set; } = "";
    }

    [Table("media")]
    public class Media
    {
        [Key]
        [MaxLength(128)]
        public virtual string id { get; set; } = "";
        [Required]
        [MaxLength(128)]
        public virtual string key { get; set; } = "";
        [Required]
        [MaxLength(64)]
        public virtual string user_id { get; set; } = "";
        [Required]
        [MaxLength(64)]
        public virtual string tweet_id { get; set; } = "";
        [Required]
        [MaxLength(1000)]
        public virtual string url { get; set; } = "";//源文件下载地址
        [Required]
        [MaxLength(500)]
        public virtual string expand_url { get; set; } = "";//在网页上展开该media的地址
        public virtual MediaType media_type { get; set; }
        [Required]
        [MaxLength(260)]
        public virtual string file_name { get; set; } = "";
        public virtual bool downloaded { get; set; } = false;
        public virtual bool readed { get; set; } = false;
        public virtual bool bookmarked { get; set; } = false;
    }

    [Table("auth_state")]
    public class AuthState
    {
        [Key]
        [MaxLength(64)]
        public virtual string Id { get; set; } = "";
        // Chrome 插件同步的浏览器凭据，避免继续依赖本地明文 twitter_auth.json 文件。
        [Column(TypeName = "mediumtext")]
        public virtual string Cookie { get; set; } = "";
        [Column(TypeName = "text")]
        public virtual string UserAgent { get; set; } = "";
        public virtual DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ExplorerFile : ExplorerFileBase
    {
        public Media media;
        public string download_dir_main;

        public ExplorerFile(Media _illust, string _download_dir)
        {
            media = _illust;
            download_dir_main = _download_dir;
            title = "";
            description = "";
            id = media.id;
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
