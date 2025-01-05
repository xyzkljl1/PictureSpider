using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PictureSpider
{
#pragma warning disable CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
    public class BaseDownloadQueue
    {
        public virtual async Task WaitForAll() { return; }
        public virtual async Task<bool> Add(string url, string dir, string file_name) { return false; }
    }
#pragma warning restore CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
}
