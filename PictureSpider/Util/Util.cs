using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PictureSpider
{
    public class Util
    {
        //返回扩展名
        public static string GetExtFromURL(string url)
        {
            var uri=new Uri(url);
            var filename = uri.Segments.Last();
            var ext = new FileInfo(filename).Extension;
            if(ext.StartsWith("."))
                ext = ext.Substring(1);
            return ext;
        }
    }
}
