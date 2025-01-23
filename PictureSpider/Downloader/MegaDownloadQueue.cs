using CG.Web.MegaApiClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.ClearScript.V8.V8CpuProfile;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace PictureSpider
{
    public class MegaDownloadQueue:BaseDownloadQueue
    {
        public MegaApiClient MegaClient=>mega;
        private MegaApiClient mega;
        private List<Task> downloading = new List<Task>();
        public MegaDownloadQueue(string proxy_access,string proxy_download)
        {
            //SNI可以访问网页，获得节点，但是无法下载(http://gfs262n333.userstorage.mega.co.nz/dl/*)
            //Go无法访问网页，在chrome上时不时可以下载，但是用curl及MegaApiClient无法下载
            mega = new MegaApiClient(new MegaWebClient(new WebProxy(proxy_access, false), new WebProxy(proxy_download, false)));
            mega.LoginAnonymous();
            //https://mega.nz/folder/CRR1FKwK#TvDSfT70WLo16AppXzIBtQ/file/GcpDQIST
            //Uri fileLink = new Uri("https://mega.nz/folder/CRR1FKwK#TvDSfT70WLo16AppXzIBtQ");
            //var nodes = (await mega.GetNodesFromLinkAsync(fileLink)).ToList();
            //await mega.DownloadFileAsync(nodes[1], "G:\\" + nodes[1].Name);
        }
        public override async Task WaitForAll() 
        {
            //await Task.WhenAll(downloading.ToArray());
            while (!CheckIfDownloadDone()) await Task.Delay(new TimeSpan(0, 1, 0));
            return;
        }
        private bool CheckIfDownloadDone()
        {
            int waiting = 0;
            int running = 0;
            int done = 0;
            int fail = 0;
            foreach (var task in downloading)
            {
                switch (task.Status)
                {
                    case TaskStatus.Created:
                    case TaskStatus.WaitingForActivation:
                    case TaskStatus.WaitingToRun:
                    case TaskStatus.WaitingForChildrenToComplete:
                        waiting++;
                        break;
                    case TaskStatus.Running:
                        running++;
                        break;
                    case TaskStatus.RanToCompletion:
                        done++;
                        break;
                    case TaskStatus.Faulted:
                    case TaskStatus.Canceled:
                        fail++;
                        break;
                }
            }
            Console.WriteLine($"[Mega] {waiting} Wait/{running} Run/{done} Done/{fail} Fail");
            return running+waiting==0;
        }
        public async Task DownloadTask(string url, string dir, string file_name)
        {
            var uri = new Uri(url);
            try
            {
                if (uri.AbsolutePath.StartsWith("/file/"))//单个文件
                    mega.DownloadFile(new Uri(url), Path.Combine(dir, file_name));
                    //await mega.DownloadFileAsync(new Uri(url), Path.Combine(dir, file_name));
                else if (uri.AbsolutePath.StartsWith("/folder/") && uri.Fragment.Contains("/file/"))
                {
                    //形如https://mega.nz/folder/2NhyhAKQ#M-r20w5Zlo8UaFp2BBVcQg/file/DIQmGT7Z
                    //不能直接下载，需要从父节点获得子节点再下载
                    var fileId = uri.Fragment.Substring(uri.Fragment.IndexOf("/file/") + "/file/".Length);
                    foreach (var node in await mega.GetNodesFromLinkAsync(new Uri(url)))
                        if (node.Id == fileId)
                        {
                            //await mega.DownloadFileAsync(node, Path.Combine(dir, file_name));
                            mega.DownloadFile(node, Path.Combine(dir, file_name));
                            break;
                        }
                }
                return;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"[Mega] Fail to download :{e.Message}/{url}");
                throw;
            }

            throw new TopLevelException($"Can't Resolve Downloaad Link:{url}");
        }
#pragma warning disable CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
        public override async Task<bool> Add(string url, string dir, string file_name)
        {
            downloading.Add(DownloadTask(url, dir, file_name));
            return true;
        }
#pragma warning restore CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
    }
}
