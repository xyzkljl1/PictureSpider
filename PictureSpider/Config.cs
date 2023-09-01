using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PictureSpider
{
    public class Config
    {
        public Config() { }
        public string Proxy = "127.0.0.1:1196";
        public string ProxyGo = "127.0.0.1:8000";
        public string PixivUserName = "Name";
        public string PixivUserId = "0";
        public string PixivDownloadDir = "./";
        public string PixivConnectStr = "server=127.0.0.1;port=4321;UID=root;pwd=pixivAss;database=pass;";
        public string TwitterConnectStr = "server=127.0.0.1;port=4321;UID=root;pwd=pixivAss;database=twitter;";
        //public string TwitterAPIKey = "";
        //public string TwitterAPISecret = "";
        public string TwitterBearerToken = "";
        public string TwitterDownloadDir = "./";
        public string TwitterUserName = "";
        public string TwitterPassword = "";
        public bool ShowInitButton = false;
    }
}
