using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PictureSpider
{
/*    public class MyList<Work> : List<TypicalWork> where Work : TypicalWork
    {
        public MyList() { }
    }*/
    public class TypicalWorkBase:BaseWork { }
    public class TypicalWorkGroupBase 
    {
        [Key]
        public virtual int Id { get; set; }
        public virtual string title { get; set; } = "";
    }
    public class TypicalUserBase : BaseUser { }
    public abstract class TypicalExplorerFileBase : ExplorerFileBase 
    {
    }

    public class TypicalWork<WorkGroupType>: TypicalWorkBase where WorkGroupType:TypicalWorkGroupBase
    {
        [Key]
        public virtual int Id { get; set; }
        public virtual string url { get; set; } = "";
        //包含.的小写后缀
        public virtual string ext { get; set; } = "";
        public virtual string title { get; set; } = "";
        //页号        
        public virtual int index { get; set; } = -1;
        public virtual bool downloaded { get; set; } = false;
        public virtual string fileName { get; set; }
        //外键, 实际存储的类型，子类如果使用了TypicalWorkGroup的子类，则应当新建一个NotMapped的workGroup属性用于返回TypicalWorkGroup的子类
        public virtual WorkGroupType workGroup { get; set; }
        [NotMapped]
        public override string TmpSubPath { get => $"{workGroup.Id}_{workGroup.title}/{fileName}{ext}"; }
        [NotMapped]
        public override string Ext { get => ext; }
        [NotMapped]
        public override bool fav { get; set; }
        [NotMapped]
        public override bool readed { get; set; }
    }
    public class TypicalWorkGroup<WorkType,UserType> : TypicalWorkGroupBase where WorkType : TypicalWorkBase where UserType: TypicalUserBase
    {
        public virtual bool readed { get; set; } = false;
        public virtual bool fav { get; set; } = false;
        //已经fetch过
        public virtual bool fetched { get; set; } = false;
        //一对多外键(导航属性),自动创建不需要显示声明[ForeignKey()]和UserId,必须是virtual
        public virtual UserType user { get; set; }
        //外键, 实际存储的类型，子类如果使用了TypicalWork的子类，则应当新建一个NotMapped的works属性用于返回TypicalWork的子类
        public virtual ICollection<WorkType> works { get; set; }
    }
    public class TypicalUser<WorkGroupType> : TypicalUserBase where WorkGroupType : TypicalWorkGroupBase
    {
        [Key]
        public virtual int Id { get; set; }
        public virtual string name { get; set; }
        //一对多外键，需要virtual ICollection
        public virtual ICollection<WorkGroupType> workGroups { get; set; }
        public TypicalUser() { }
        public TypicalUser(string _name) { name = _name; }
    }
    // 在TypicalServer中没有用到，日后使用
    public class TypicalExplorerFile<WorkType,WorkGroupType,UserType> : TypicalExplorerFileBase
        where WorkType : TypicalWork<WorkGroupType>
        where WorkGroupType : TypicalWorkGroup<WorkType, UserType>
        where UserType : TypicalUser<WorkGroupType>
    {
        //基类中定义的属性在基类中修改，未定义的在illust中
        public WorkGroupType workGroup;
        public List<WorkType> sortedIllusts;
        public string download_dir_tmp;

        public TypicalExplorerFile(WorkGroupType _workGroup, string _download_dir)
        {
            workGroup = _workGroup;
            download_dir_tmp = _download_dir;
            title = workGroup.title;
            id = workGroup.Id.ToString();
            userId = workGroup.user.name;
            bookmarked = workGroup.fav;
            readed = workGroup.readed;
            sortedIllusts = workGroup.works.ToList();
            sortedIllusts.Sort((x, y) => x.index.CompareTo(y.index));
        }
        public override string FilePath(int page)
        {
            return Path.Combine(download_dir_tmp, sortedIllusts[page].fileName + sortedIllusts[page].ext);
        }

        public override int pageCount() { return workGroup.works.Count; }

        public override string WebsiteURL(int page) { return "";/*return $"https://hitomi.la/reader/{workGroup.Id}.html#{page}"; */}

        public override int validPageCount() { return 0;/*return workGroup._works.Count(x => !x.excluded); */}

        public override bool isPageValid(int page) { return !sortedIllusts[page].excluded; }
        public override void switchPageValid(int page)
        {
            sortedIllusts[page].excluded = !sortedIllusts[page].excluded;
        }
    }
    public class TypicalDatabase<WorkType,WorkGroupType,UserType> : BaseEFDatabase 
        where WorkType:TypicalWork<WorkGroupType>
        where WorkGroupType:TypicalWorkGroup<WorkType,UserType>
        where UserType: TypicalUser<WorkGroupType>
    {
        public DbSet<WorkType> Works { get; set; }
        public DbSet<WorkGroupType> WorkGroups { get; set; }
        public DbSet<UserType> Users { get; set; }
    }
}
