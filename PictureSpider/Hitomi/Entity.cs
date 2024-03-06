using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PictureSpider.Hitomi
{
    [Table("Illusts")]
    public class Illust
    {
        //不同图片hash可能会重复，例如汉化组在最后加的页面因此使用自增Id
        //在database.OnModelCreating中开启了级联删除
        //但是注意级联删除只在删除IllustGroup时，清空导航属性(IllustGroup.illusts。Clear)时不会删除，只会把外键置为null
        [Key]
        public int Id { get; set; }
        public string hash { get; set; }
        [NotMapped]
        public string url { get; set; } = "";
        //注意ext虽然是由url决定，但是需要保存，因为检查本地文件存在时需要ext
        //必须小写
        public string ext { get; set; } = "";
        //页号        
        public int index { get; set; } = -1;
        //文件名,应当考虑到使用其它方式浏览时的排序
        public string fileName { get; set; }
        //排除，只有对于fav的IllustGroup的Illust，这一项才有效
        public bool excluded { get; set; }=false;
        //外键
        public virtual IllustGroup illustGroup { get; set; }
        //根据url获得ext,注意并未调用database.SaveChanges();
        public void ResetEXTByURL()
        {
            ext = "";
            var pos = url.LastIndexOf('.');
            if (pos > 0)
                ext = url.Substring(pos).ToLower();
        }
    }
    [Table("IllustGroups")]
    public class IllustGroup
    {
        [Key]
        public int Id { get; set; }
        public string title { get; set; }
        public bool readed { get; set; } = false;
        public bool fav { get; set; } = false;
        //已经fetch过
        public bool fetched { get; set; } = false;
        //一对多外键(导航属性),自动创建不需要显示声明[ForeignKey()]和UserId,必须是virtual
        public virtual User user { get; set; }
        //外键
        public virtual ICollection<Illust> illusts { get; set; }
    }
    [Table("Users")]
    public class User : BaseUser
    {
        //Hitomi.la以用户名作id
        [Key]
        public string name { get; set; }
        //一对多外键，需要virtual ICollection
        public virtual ICollection<IllustGroup> illustGroups { get; set; }

        public User() { }
        public User(string _name) { name = _name; }
    }
    public class ExplorerFile : ExplorerFileBase
    {
        //基类中定义的属性在基类中修改，未定义的在illust中
        public IllustGroup illustGroup;
        public List<Illust> sortedIllusts;
        public string download_dir_tmp;
        public ExplorerFile(IllustGroup _illustGroup, string _download_dir)
        {
            illustGroup = _illustGroup;
            download_dir_tmp = _download_dir;
            title = illustGroup.title;
            id = illustGroup.Id.ToString();
            userId = illustGroup.user.name;
            bookmarked = illustGroup.fav;
            readed = illustGroup.readed;
            sortedIllusts = illustGroup.illusts.ToList();
            sortedIllusts.Sort((x, y) => x.index.CompareTo(y.index));
        }
        public override string FilePath(int page)
        {
            return Path.Combine(download_dir_tmp, sortedIllusts[page].fileName+sortedIllusts[page].ext);
        }

        public override int pageCount() { return illustGroup.illusts.Count; }

        public override string WebsiteURL(int page) { return $"https://hitomi.la/reader/{illustGroup.Id}.html#{page}"; }

        public override int validPageCount() { return illustGroup.illusts.Count(x=>!x.excluded); }

        public override bool isPageValid(int page) { return !sortedIllusts[page].excluded; }
        public override void switchPageValid(int page) { 
            sortedIllusts[page].excluded= sortedIllusts[page].excluded;
        }
    }
}
