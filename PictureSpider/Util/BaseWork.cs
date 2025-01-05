using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PictureSpider.Downloader;

namespace PictureSpider
{
    public class BaseWork
    {
        [NotMapped]
        public virtual string TmpSubPath { get; }
        [NotMapped]
        public virtual string FavSubPath { get; }
        [NotMapped]
        public virtual string DownloadURL { get; }
        [NotMapped]
        public virtual DownloaderType GetDownloader => DownloaderType.Aria2DownloadQueue;
        //包含.的小写后缀
        [NotMapped]
        public virtual string Ext { get => Path.GetExtension(TmpSubPath).ToLower(); }

        public virtual bool fav { get; set; } = false;
        public virtual bool readed { get; set; } = false;
        public virtual bool excluded { get; set; } = false;//属于fav的group(如果有group)但是单独排除该work
    }
}
