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
        [StringLength(600)]
        public string path { get; set; }
        [StringLength(600)]
        public string sub_path { get; set; }
        public DateTime date { get; set; }
        //数据库内的文件要么fav要么readed，其它状态的不会进入数据库，因此fav和readed共用一个变量存储
        public bool fav { get; set; }
        [NotMapped]
        public bool readed { get { return !fav; } set { fav = !value; } }
        public Illust() { }
        public Illust(string _path,string _sub_path,bool _fav)
        {
            path = _path;
            sub_path = _sub_path;
            fav = _fav;
            date = DateTime.Now;
        }
    }

    public class ExplorerFile : ExplorerFileBase
    {
        public string path;
        //相对于根目录(如FavDir)的路径，为了在从tmp移动到fav时保持目录结构
        public string sub_path;
        //是否位于fav目录(即创建时的bookmarked值)，这会影响后续操作
        //原本位于fav目录的文件，取消fav->fav后，应当原地不动
        //原本不位于fav目录的文件，fav->取消fav->fav,应当加入待处理队列
        public bool in_fav_dir=false;
        public ExplorerFile(string _path,string _root_path,bool _b)
        {
            path = _path;
            sub_path= Path.GetRelativePath(_root_path, _path);
            description = sub_path.Replace("\\","<br>");
            in_fav_dir = bookmarked = _b;
            readed = false;
        }
        public override string FilePath(int page) { return path; }
        public override int pageCount() { return 1; }
        public override string WebsiteURL(int page) { return ""; }
        public override int validPageCount() { return 1; }
        public override bool isPageValid(int page) { return true; }
    }
}
