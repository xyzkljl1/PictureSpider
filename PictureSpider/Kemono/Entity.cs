using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static TdLib.TdApi;

namespace PictureSpider.Kemono
{
    //attachment没有id，用path+service确定
    [PrimaryKey(nameof(urlPath), nameof(service))]
    [Table("Works")]
    public class Work:BaseWork
    {
        public string name { get; set; }//注意name可能是个文件名也可能是个带文件名的网址
        public string service { get; set; }//不确定service来自于coverGroup还是workGroup,需要存储一份
        public string urlPath { get; set; }
        public string urlHost { get; set; }
        [NotMapped]
        public WorkGroup GetGroup
        {
            get=> workGroup??coverGroup;
        }
        [NotMapped]
        public override string Ext { get => Path.GetExtension(name).ToLower(); }//改成从name获取防止循环引用

        [NotMapped]
        public override string TmpSubPath
        {
            get
            {
                if(Ext.IsVideo())
                    return $"{GetGroup.user.id}/{service}_{GetGroup.id}_{index}_{Path.GetFileName(name)}";
                return $"{service}/{GetGroup.user.id}/{GetGroup.id}/{index}_{Path.GetFileName(name)}";
            }
        }
        [NotMapped]
        public override string FavSubPath
        {
            get
            {
                return $"{GetGroup.user.displayText}/{service}/{GetGroup.id}/{index}_{Path.GetFileName(name)}";
            }
        }

        [NotMapped]
        public override string DownloadURL
        {
            get
            {
                if (urlHost is not null&&urlHost!= "")
                    return $"{urlHost}/data{urlPath}";
                //没有server时随便用n1~n4中的一个
                return $"https://n4.kemono.su/data{urlPath}";
            }
        }
        //页号
        public int index { get; set; } = -1;
        //由于Work通过cover和works分别关联到WorkGroup，需要手动指定哪个外键对应哪个关联关系
        [ForeignKey("workGroupid,workGroupuserservice")]
        public virtual WorkGroup workGroup { get; set; }
        [ForeignKey("coverGroupid,coverGroupuserservice")]
        [AllowNull]
        public virtual WorkGroup coverGroup { get; set; }
        public Work() { }
    }
    //desc中附带的外链
    //尚未实现
    [PrimaryKey(nameof(id), nameof(type))]
    [Table("ExternalWorks")]
    public class ExternalWork: BaseWork
    {
        public enum ExternalWorkType
        {
            Mega=0,
        }
        public string id { get; set; }
        public string name { get; set; }
        [NotMapped]
        public string service { get { return workGroup.service; } }
        public string url { get; set; }
        public ExternalWorkType type { get; set; }

        [NotMapped]
        public override string Ext { get => Path.GetExtension(name).ToLower(); }//改成从name获取防止循环引用

        [NotMapped]
        public override string TmpSubPath
        {
            get
            {
                if(Ext.IsVideo())//目前客户端不能浏览视频，所以尽量放在同一级目录以便使用外部目录浏览
                    return $"{workGroup.user.id}/{service}_{workGroup.id}_{index}_{Path.GetFileName(name)}";
                return $"{service}/{workGroup.user.id}/{workGroup.id}/{index}_{Path.GetFileName(name)}";
            }
        }
        [NotMapped]
        public override string FavSubPath
        {
            get
            {
                if(Ext.IsVideo())
                    return $"{workGroup.user.displayText}/{service}_{workGroup.id}_{index}_{Path.GetFileName(name)}";
                return $"{workGroup.user.displayText}/{service}/{workGroup.id}/{index}_{Path.GetFileName(name)}";
            }
        }
        [NotMapped]
        public override string DownloadURL => url;
        [NotMapped]
        public override Downloader.DownloaderType GetDownloader => Downloader.DownloaderType.MegaDownloadQueue;
        //页号
        public int index { get; set; } = -1;
        public virtual WorkGroup workGroup { get; set; }
    }
    [PrimaryKey(nameof(id), "userservice")]//userservice是自动生成的对user的外键
    [Table("WorkGroups")]
    public class WorkGroup
    {
        public string id { get; set; }
        public string title { get; set; }
        [NotMapped]
        public string service { get { return user.service; }}
        public string desc { get; set; }
        public string embedUrl { get; set; }//例:https://kemono.su/patreon/user/3659577/post/55491373
        public bool readed { get; set; } = false;
        public bool fav { get; set; } = false;
        //已经fetch过
        public bool fetched { get; set; } = false;

        [ForeignKey("userid,userservice")]
        public virtual User user { get; set; }
        public Work cover { get; set; }
        public virtual ICollection<Work> works { get; set; }
        public virtual ICollection<ExternalWork> externalWorks { get; set; }
    }
    [Table("Users")]
    [PrimaryKey(nameof(id), nameof(service))]
    public class User : BaseUser
    {
        //注意此id为原网站id，不保证不同service无重复，也不能保证在int范围内
        //必须id+service才能确定一个作者,group和illust同理
        public string id { get;set; }
        public string service { get; set; }
        //public string relation_id { get; set; }//用途不明
        public bool dowloadExternalWorks { get; set; } = false;//未实现
        //public bool dowloadCover { get; set; } = false;
        public bool dowloadWorks { get; set; } = true;
        public bool dowloadEmbed { get; set; } = true;//未实现
        public DateTime fetchedTime { get; set; }//此时间以前的已经fetch过了

        //一对多外键，需要virtual ICollection
        public virtual ICollection<WorkGroup> workGroups { get; set; }

        public User() { }
    }
    public class ExplorerFile : ExplorerFileBase
    {
        //基类中定义的属性在基类中修改，未定义的在illust中
        public WorkGroup illustGroup;
        public List<Work> sortedIllusts;
        public string download_dir_tmp;
        public ExplorerFile(WorkGroup _illustGroup, string _download_dir)
        {
            illustGroup = _illustGroup;
            download_dir_tmp = _download_dir;
            title = illustGroup.title;
            id = illustGroup.id.ToString();
            userId = $"{illustGroup.user.id}";
            bookmarked = illustGroup.fav;
            readed = illustGroup.readed;
            sortedIllusts = illustGroup.works.ToList();
            sortedIllusts.Sort((x, y) => x.index.CompareTo(y.index));
        }
        public override string FilePath(int page)
        {
            return Path.Combine(download_dir_tmp, sortedIllusts[page].TmpSubPath);
        }

        public override int pageCount() { return illustGroup.works.Count; }

        public override string WebsiteURL(int page) { return $"https://kemono.su/{illustGroup.service}/user/{illustGroup.user.id}/post/{illustGroup.id}"; }

        public override int validPageCount() { return illustGroup.works.Count(x => !x.excluded); }

        public override bool isPageValid(int page) { return !sortedIllusts[page].excluded; }
        public override void switchPageValid(int page)
        {
            sortedIllusts[page].excluded = !sortedIllusts[page].excluded;
        }
    }
    public class ExplorerExternalFile : ExplorerFileBase
    {
        //基类中定义的属性在基类中修改，未定义的在illust中
        public WorkGroup illustGroup;
        public List<ExternalWork> sortedIllusts;
        public string download_dir_tmp;
        public ExplorerExternalFile(WorkGroup _illustGroup, string _download_dir)
        {
            illustGroup = _illustGroup;
            download_dir_tmp = _download_dir;
            title = illustGroup.title;
            id = illustGroup.id.ToString();
            userId = $"{illustGroup.user.id}";
            bookmarked = illustGroup.fav;
            readed = illustGroup.readed;
            sortedIllusts = illustGroup.externalWorks.ToList();
            sortedIllusts.Sort((x, y) => x.index.CompareTo(y.index));
        }
        public override string FilePath(int page)
        {
            //TODO
            return Path.Combine(download_dir_tmp, sortedIllusts[page].name);
        }

        public override int pageCount() { return illustGroup.works.Count; }

        public override string WebsiteURL(int page) { return $"https://kemono.su/{illustGroup.service}/user/{illustGroup.user.id}/post/{illustGroup.id}"; }

        public override int validPageCount() { return illustGroup.works.Count(x => !x.excluded); }

        public override bool isPageValid(int page) { return !sortedIllusts[page].excluded; }
        public override void switchPageValid(int page)
        {
            sortedIllusts[page].excluded = !sortedIllusts[page].excluded;
        }
    }
}
