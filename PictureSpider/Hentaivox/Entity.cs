using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PictureSpider.Hentaivox
{
    [Table("Works")]
    public class Work : TypicalWork<WorkGroup> {}
    [Table("WorkGroups")]
    public class WorkGroup : TypicalWorkGroup<Work,User> {}
    [Table("Users")]
    public class User : TypicalUser<WorkGroup> {}
    public class Database : TypicalDatabase<Work, WorkGroup, User> { }
}
