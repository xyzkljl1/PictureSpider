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
        public string ProxySNI = "127.0.0.1:1200";//能实现绕过SNI的本地代理，目前使用Accesser
        public string ProxyGo = "127.0.0.1:8000";
        public string PixivUserName = "Name";
        public string PixivUserId = "0";
        public string PixivDownloadDir = "./";
        public string PixivConnectStr = "";
        public string TwitterConnectStr = "";
        public string LSFConnectStr = "";
        public string LSFFavDir = "./";
        public List<String> LSFTmpDirs = new List<String>();
        public string HitomiConnectStr = "";
        public string HitomiDownloadDir = "./";
        //public string TwitterAPIKey = "";
        //public string TwitterAPISecret = "";
        public string TwitterBearerToken = "";
        public string TwitterDownloadDir = "./";
        public string TwitterUserName = "";
        public string TwitterPassword = "";
        public bool ShowInitButton = false;
        public string TelegramPhoneNumber = "";
        public string TelegramDownloadDir = "";
        public int TelegramApiID = 0;
        public string TelegramApiHash = "";
        public string TelegramConnectStr = "";
        public string MyDownloadServerAddress = "";
    }
}
