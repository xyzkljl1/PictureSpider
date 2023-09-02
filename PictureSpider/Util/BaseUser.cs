using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PictureSpider
{
    public enum TagStatus
    {
        None,
        Follow,
        Ignore
    };
    public class BaseUser
    {
        [Required]
        //[DatabaseGenerated(DatabaseGeneratedOption.Identity)][DefaultValue("123")]
        //直接[DefualtValue]并没有卵用，参考https://stackoverflow.com/questions/19554050/entity-framework-6-code-first-default-value/34894274
        [MaxLength(128)]
        public string displayId { get; set; }//区分队列的id,在不同Server中意义不同
        [Required]
        [MaxLength(128)]
        public string displayText { get; set; }//显示在UI上的队列名
        [Required]
        public bool followed { get; set; } = false;
        [Required]
        public bool queued { get; set; } = false;
    }
}
