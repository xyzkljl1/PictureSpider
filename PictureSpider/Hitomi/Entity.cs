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
        public int Id { get; set; }
        [Required]
        public string PageURL { get; set; } = "";

    }
    [Table("IllustGroups")]
    public class IllustGroup
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string PageURL { get; set; } = "";

    }
}
