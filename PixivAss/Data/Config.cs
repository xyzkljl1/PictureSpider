using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PixivAss.Data
{
    class Config
    {
        public Config() { }
        public string Proxy = "127.0.0.1:1081";
        public string UserName = "Name";
        public string UserId = "0";
        public string DownloadDir = "./";
        public string ConnectStr = "server=127.0.0.1;port=4321;UID=root;pwd=pixivAss;database=pass;";
        public bool ShowInitButton = false;
    }
}
