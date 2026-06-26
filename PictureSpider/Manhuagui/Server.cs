using System;
using System.Threading.Tasks;

namespace PictureSpider.Manhuagui
{
    public class Server : BaseServer, IDisposable
    {
        private readonly string downloadDir;

        public Server(Config config)
        {
            logPrefix = "MG";
            downloadDir = config.ManhuaguiDownloadDir;
            Util.TouchDir(downloadDir);
        }

        public void Dispose()
        {
        }

        public override Task Init()
        {
            return Task.CompletedTask;
        }
    }
}
