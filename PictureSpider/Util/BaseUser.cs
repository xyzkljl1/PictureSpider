using System;
using System.Collections.Generic;
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
        public string displayId;//区分队列的id,在不同Server中意义不同
        public string displayText;//显示在UI上的队列名
        public bool followed { get; set; }
        public bool queued { get; set; }
    }
}
