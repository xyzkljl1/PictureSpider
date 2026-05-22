using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PictureSpider
{
#pragma warning disable CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
    public class DownloadRequestOptions
    {
        public string Referer { get; set; } = "";
        public string UserAgent { get; set; } = "";
        public string Cookie { get; set; } = "";
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
        public int? Split { get; set; }
        public int? MaxConnectionPerServer { get; set; }
    }

    public class BaseDownloadQueue
    {
        public virtual async Task WaitForAll() { return; }
        public virtual async Task<bool> Add(string url, string dir, string file_name) { return false; }
        public virtual async Task<bool> Add(string url, string dir, string file_name, DownloadRequestOptions options)
        {
            return await Add(url, dir, file_name);
        }
    }
#pragma warning restore CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
}
