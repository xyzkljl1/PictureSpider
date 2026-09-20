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

namespace PictureSpider.Pawchive
{
    public class PawchiveBaseWork: BaseWork {
        // 表示交由外部程序管理，本程序只负责下载，用于非图片类型文件
        [NotMapped]
        public bool Dettached
        {
            get { return !Ext.IsImage(); }
        }
        // 对于Detach类型，用readed表示已经下载过至少一次
        [NotMapped]
        public bool DettachDownloaded
        {
            get { return readed; }
            set { readed = value; }
        }
    }
    //attachment没有id，用path+service确定
    [PrimaryKey(nameof(urlPath), nameof(service))]
    [Table("Works")]
    public class Work: PawchiveBaseWork
    {
        public string name { get; set; }//注意name可能是个文件名也可能是个带文件名的网址
        public string service { get; set; }//不确定service来自于coverGroup还是workGroup,需要存储一份
        public string urlPath { get; set; }
        [NotMapped]
        public WorkGroup GetGroup
        {
            get=> workGroup??coverGroup;
        }
        [NotMapped]
        public override string Ext { get => Path.GetExtension(name ?? "").ToLower(); }//改成从name获取防止循环引用

        [NotMapped]
        public override string TmpSubPath
        {
            get
            {
                var group = GetGroup.ParentGroup;
                // 视频类不通过此程序预览，需要用用户名作目录
                if(Ext.IsVideo())
                    return $"{group.user.id}_{Util.ReplaceInvalidCharInFilenameWithReturnValue(group.user.displayText)}/{service}_{group.id}_{index}_{Path.GetFileName(name)}";
                return $"{service}/{group.user.id}/{group.id}/{index}_{Path.GetFileName(name)}";
            }
        }
        [NotMapped]
        public override string FavSubPath
        {
            get
            {
                var group = GetGroup.ParentGroup;
                return $"{group.user.displayText}/{service}/{group.id}/{index}_{Path.GetFileName(name)}";
            }
        }

        [NotMapped]
        public override string DownloadURL => $"https://file.{Server.baseHost}/data{urlPath}?f={Uri.EscapeDataString(name)}";
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
    public class ExternalWork: PawchiveBaseWork
    {
        public enum ExternalWorkType
        {
            Mega=0,
            GoogleDrive=1,
        }
        public string id { get; set; }
        public string name { get; set; }
        /*
        // 表示交由外部程序管理，本程序只负责下载，用于非图片类型文件
        [NotMapped]
        public bool Dettached
        {
            get { return !Ext.IsImage(); }
        }*/
        [NotMapped]
        public string service { get { return workGroup.service; } }
        public string url { get; set; }
        public ExternalWorkType type { get; set; }

        [NotMapped]
        public override string Ext { get => Path.GetExtension(name ?? "").ToLower(); }//改成从name获取防止循环引用

        [NotMapped]
        public override string TmpSubPath
        {
            get
            {
                var group = workGroup.ParentGroup;
                if(Ext.IsVideo())//目前客户端不能浏览视频，所以尽量放在同一级目录以便使用外部目录浏览
                    return $"{group.user.id}_{Util.ReplaceInvalidCharInFilenameWithReturnValue(group.user.displayText)}/{service}_{group.id}_{index}_{Path.GetFileName(name)}";
                return $"{service}/{group.user.id}/{group.id}/{index}_{Path.GetFileName(name)}";
            }
        }
        [NotMapped]
        public override string FavSubPath
        {
            get
            {
                var group = workGroup.ParentGroup;
                if(Ext.IsVideo())
                    return $"{group.user.displayText}/{service}_{group.id}_{index}_{Path.GetFileName(name)}";
                return $"{group.user.displayText}/{service}/{group.id}/{index}_{Path.GetFileName(name)}";
            }
        }
        [NotMapped]
        public override string DownloadURL => url;
        [NotMapped]
        public override Downloader.DownloaderType GetDownloader => type switch
        {
            ExternalWorkType.Mega => Downloader.DownloaderType.MegaDownloadQueue,
            ExternalWorkType.GoogleDrive => Downloader.DownloaderType.GoogleDriveDownloadQueue,
            _ => throw new NotSupportedException($"Unsupported external work type: {type}")
        };
        //页号
        public int index { get; set; } = -1;
        public virtual WorkGroup workGroup { get; set; }
    }
    [PrimaryKey(nameof(id), "userservice")]//userservice是自动生成的对user的外键
    [Table("WorkGroups")]
    public class WorkGroup : IHasReadFav
    {
        public string id { get; set; }
        public string title { get; set; }
        // 父组只包含图片；子组包含非图片文件
        public string parentId { get; set; }
        public virtual WorkGroup parent { get; set; }
        public virtual WorkGroup child { get; set; }
        [NotMapped]
        public bool IsChild => parentId != null;
        [NotMapped]
        public WorkGroup ParentGroup => IsChild ? parent : this;
        [NotMapped]
        public bool DettachDownloaded
        {
            get { return readed; }
            set { readed = value; }
        }
        [NotMapped]
        public string service { get { return user.service; }}
        public string desc { get; set; }
        public string embedUrl { get; set; }//例:patreon/user/3659577/post/55491373
        public bool readed { get; set; } = false;
        public bool fav { get; set; } = false;
        //已经fetch过
        public bool fetched { get; set; } = false;

        [ForeignKey("userid,userservice")]
        public virtual User user { get; set; }
        public virtual Work cover { get; set; }
        public virtual ICollection<Work> works { get; set; } = new List<Work>();
        public virtual ICollection<ExternalWork> externalWorks { get; set; } = new List<ExternalWork>();

        public IEnumerable<PawchiveBaseWork> GetShouldDownloadWorks()
        {
            var selected = works.Where(work =>
                (user.downloadAttachmentVideos && work.Ext.IsVideo()) ||
                (user.downloadAttachmentImages && work.Ext.IsImage())).Cast<PawchiveBaseWork>();
            if (user.dowloadExternalWorks != User.DownloadExternalWorkType.None)
                selected = selected.Concat(externalWorks);
            return selected.Where(work => !fav || !work.excluded);
        }
    }
    [Table("Users")]
    [PrimaryKey(nameof(id), nameof(service))]
    public class User : BaseUserEx
    {
        public enum DownloadExternalWorkType
        {
            None=0,
            DirectExternal=1,
            KeyZipMega=2,
        }
        //注意此id为原网站id，不保证不同service无重复，也不能保证在int范围内
        //必须id+service才能确定一个作者,group和illust同理
        public string id { get;set; }
        public string service { get; set; }
        //public string relation_id { get; set; }//用途不明
        public DownloadExternalWorkType dowloadExternalWorks { get; set; } = DownloadExternalWorkType.None;
        //public bool dowloadCover { get; set; } = false;
        public bool downloadAttachmentVideos { get; set; } = false;
        public bool downloadAttachmentImages { get; set; } = true;
        public bool dowloadEmbed { get; set; } = true;//未实现
        public DateTime fetchedTime { get; set; }//此时间以前的已经fetch过了

        [DbKey]
        [NotMapped]
        public string UserDbKey => $"{service}/{id}";

        //一对多外键，需要virtual ICollection
        public virtual ICollection<WorkGroup> workGroups { get; set; }

        public User() { }
    }
    public class ExplorerFile : ExplorerFileBaseEx
    {
        //基类中定义的属性在基类中修改，未定义的在illust中
        public WorkGroup illustGroup;
        public List<Work> sortedIllusts;
        public string download_dir_tmp;
        [DbKey]
        [NotMapped]
        public string WorkGroupDbKey => $"{illustGroup.service}/{illustGroup.id}";
        public ExplorerFile(WorkGroup _illustGroup, string _download_dir)
        {
            illustGroup = _illustGroup;
            download_dir_tmp = _download_dir;
            title = illustGroup.title;
            id = illustGroup.id.ToString();
            userId = illustGroup.user.DbKey;
            bookmarked = illustGroup.fav;
            readed = illustGroup.readed;
            sortedIllusts = illustGroup.works.Where(x=>x.Ext.IsImage()).ToList();
            foreach (var work in sortedIllusts)
                work.workGroup ??= illustGroup;
            sortedIllusts.Sort((x, y) => x.index.CompareTo(y.index));
        }
        public override string FilePath(int page)
        {
            return Path.Combine(download_dir_tmp, sortedIllusts[page].TmpSubPath);
        }

        public override int pageCount() { return sortedIllusts.Count; }

        public override string WebsiteURL(int page) { return $"{Server.baseUrl}/{illustGroup.service}/user/{illustGroup.user.id}/post/{illustGroup.id}"; }

        public override int validPageCount() { return sortedIllusts.Count(x => !x.excluded); }

        public override bool isPageValid(int page) { return !sortedIllusts[page].excluded; }
        public override void switchPageValid(int page)
        {
            sortedIllusts[page].excluded = !sortedIllusts[page].excluded;
        }
        public override string GetPageDbKey(int page)
        {
            if (page < 0 || page >= sortedIllusts.Count)
                throw new ArgumentOutOfRangeException(nameof(page));
            return $"Work|{sortedIllusts[page].service}|{sortedIllusts[page].urlPath}";
        }
    }
    public class ExplorerExternalFile : ExplorerFileBaseEx
    {
        //基类中定义的属性在基类中修改，未定义的在illust中
        public WorkGroup illustGroup;
        public List<ExternalWork> sortedIllusts;
        public string download_dir_tmp;
        [DbKey]
        [NotMapped]
        public string WorkGroupDbKey => $"{illustGroup.service}/{illustGroup.id}";
        public ExplorerExternalFile(WorkGroup _illustGroup, string _download_dir)
        {
            illustGroup = _illustGroup;
            download_dir_tmp = _download_dir;
            title = illustGroup.title;
            id = illustGroup.id.ToString();
            userId = illustGroup.user.DbKey;
            bookmarked = illustGroup.fav;
            readed = illustGroup.readed;
            sortedIllusts = illustGroup.externalWorks.Where(x => x.Ext.IsImage()).ToList();
            foreach (var work in sortedIllusts)
                work.workGroup ??= illustGroup;
            sortedIllusts.Sort((x, y) => x.index.CompareTo(y.index));
        }
        public override string FilePath(int page)
        {
            //TODO
            return Path.Combine(download_dir_tmp, sortedIllusts[page].name);
        }

        public override int pageCount() { return sortedIllusts.Count; }

        public override string WebsiteURL(int page) { return $"{Server.baseUrl}/{illustGroup.service}/user/{illustGroup.user.id}/post/{illustGroup.id}"; }

        public override int validPageCount() { return sortedIllusts.Count(x => !x.excluded); }

        public override bool isPageValid(int page) { return !sortedIllusts[page].excluded; }
        public override void switchPageValid(int page)
        {
            sortedIllusts[page].excluded = !sortedIllusts[page].excluded;
        }
        public override string GetPageDbKey(int page)
        {
            if (page < 0 || page >= sortedIllusts.Count)
                throw new ArgumentOutOfRangeException(nameof(page));
            return $"ExternalWork|{(int)sortedIllusts[page].type}|{sortedIllusts[page].id}";
        }
    }
}
