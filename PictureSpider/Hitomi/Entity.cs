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
        [Key]
        public string hash { get; set; }
        public string url { get; set; }
        //外键
        public virtual IllustGroup illustGroup { get; set; }
    }
    [Table("IllustGroups")]
    public class IllustGroup
    {
        [Key]
        public int id { get; set; }
        public string PageURL { get; set; }
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
        public string id { get; set; }
        //一对多外键，需要virtual ICollection
        public virtual ICollection<IllustGroup> illustGroups { get; set; }

        public User() { }
    }
}
