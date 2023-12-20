﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PictureSpider.LocalSingleFile
{
    //待处理的文件
    //本地文件不存入数据库，只存储需要删除但尚未删除、需要移动但尚未移动的文件
    [Table("Waited")]
    public class Illust
    {
        [Key]
        public string path { get; set; }
        public DateTime date { get; set; }
        //数据库内的文件要么fav要么readed，其它状态的不会进入数据库，因此fav和readed共用一个变量存储
        public bool fav { get; set; }
        [NotMapped]
        public bool readed { get { return !fav; } set { fav = !value; } }
        public Illust() { }
        public Illust(string _p,bool _f)
        {
            path = _p;
            fav = _f;
            date = DateTime.Now;
        }
    }

    public class ExplorerFile : ExplorerFileBase
    {
        public string path;
        public ExplorerFile(string _path,bool _b)
        {
            path = _path;
            bookmarked = _b;
            readed = false;
        }
        public override string FilePath(int page) { return path; }
        public override int pageCount() { return 1; }
        public override string WebsiteURL(int page) { return ""; }
        public override int validPageCount() { return 1; }
        public override bool isPageValid(int page) { return true; }
    }
}
