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
    public class MegaDownloadQueue: BaseDownloadQueue
    {
        public MegaApiClient MegaClient=>mega;
        private MegaApiClient mega;
        private List<Task> downloading = new List<Task>();
        private bool loginSuccessed = false;
        public MegaDownloadQueue(string proxy_access,string proxy_download)
        {
            //SNI可以访问网页，获得节点，但是无法下载(http://gfs262n333.userstorage.mega.co.nz/dl/*)
            //Go无法访问网页，在chrome上时不时可以下载，但是用curl及MegaApiClient无法下载
            mega = new MegaApiClient(new MegaWebClient(new WebProxy(proxy_access, false), new WebProxy(proxy_download, false)));
            Task.Run(()=>{
                try
                {
                    mega.LoginAnonymous();
                    loginSuccessed = true;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[Mega] login fail {e.Message}");
                }
            });
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
            var path = Path.Combine(dir, file_name);
            var downloadPath = path + ".mega.part";
            bool downloaded = false;
            try
            {
                // 正式文件只在下载成功后出现，避免残缺文件被上层误判为已下载。
                File.Delete(downloadPath);
                if (uri.AbsolutePath.StartsWith("/file/"))//单个文件
                {
                    await mega.DownloadFileAsync(uri, downloadPath);
                    downloaded = true;
                }
                else if (uri.AbsolutePath.StartsWith("/folder/") && uri.Fragment.Contains("/file/"))
                {
                    //形如https://mega.nz/folder/2NhyhAKQ#M-r20w5Zlo8UaFp2BBVcQg/file/DIQmGT7Z
                    //不能直接下载，需要从父节点获得子节点再下载
                    var fileId = uri.Fragment.Substring(uri.Fragment.IndexOf("/file/") + "/file/".Length);
                    foreach (var node in await mega.GetNodesFromLinkAsync(new Uri(url)))
                        if (node.Id == fileId)
                        {
                            await mega.DownloadFileAsync(node, downloadPath);
                            downloaded = true;
                            break;
                        }
                }
                if (!downloaded)
                    throw new TopLevelException($"Can't Resolve Download Link:{url}");
                File.Move(downloadPath, path);
                return;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"[Mega] Fail to download :{e.Message}/{url}");
                try
                {
                    File.Delete(downloadPath);
                }
                catch (Exception cleanupException)
                {
                    Console.Error.WriteLine($"[Mega] Fail to remove temporary file:{cleanupException.Message}");
                }
                throw;
            }

        }
#pragma warning disable CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
        public override async Task<bool> Add(string url, string dir, string file_name)
        {
            if (!loginSuccessed)
            {
                Console.WriteLine($"[Mega] login fail,can't download");
                return false;
            }
            downloading.Add(DownloadTask(url, dir, file_name));
            return true;
        }
#pragma warning restore CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
    }
}
