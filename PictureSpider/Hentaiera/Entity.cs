using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PictureSpider.Hentaiera
{
    [Table("Works")]
    public class Work : TypicalWork<WorkGroup>
    {
        [NotMapped]
        public override string TmpSubPath { get => $"{workGroup.user.name.ReplaceInvalidCharInFilenameWithReturnValue()}/{base.TmpSubPath}"; }
    }
    [Table("WorkGroups")]
    public class WorkGroup : TypicalWorkGroup<Work,User> {}
    [Table("Users")]
    public class User : TypicalUser<WorkGroup> {}
    public class Database : TypicalDatabase<Work, WorkGroup, User> { }
}
