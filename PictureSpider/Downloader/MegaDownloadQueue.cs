using CG.Web.MegaApiClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Policy;
using System.Text;
using System.Threading;
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
        private readonly SemaphoreSlim availableSlots = new SemaphoreSlim(10, 10);
        private long retryAfterTicks;
        private bool loginSuccessed = false;
        public MegaDownloadQueue(string proxy_access,string proxy_download)
        {
            //SNI可以访问网页，获得节点，但是无法下载(http://gfs262n333.userstorage.mega.co.nz/dl/*)
            //Go无法访问网页，在chrome上时不时可以下载，但是用curl及MegaApiClient无法下载
            mega = new MegaApiClient(new MegaWebClient(new WebProxy(proxy_access, false), new WebProxy(proxy_download, false), PauseDownloads));
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
            string downloadPath = null;
            try
            {
                var uri = new Uri(url);
                var path = Path.Combine(dir, file_name);
                downloadPath = path + ".mega.part";
                bool downloaded = false;
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
            // 已取得下载地址，文件存储服务器在传输数据时返回带宽限流。
            catch (HttpRequestException e) when ((int?)e.StatusCode == 509)
            {
                return;
            }
            // 已确认处于带宽限流冷却期时，忽略仍在途任务返回的临时服务不可用。
            catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.ServiceUnavailable
                && Interlocked.Read(ref retryAfterTicks) > DateTime.UtcNow.Ticks)
            {
                return;
            }
            // 获取下载地址等信息时，MEGA API直接返回带宽配额已耗尽。
            catch (ApiException e) when (e.ApiResultCode == ApiResultCode.QuotaExceeded)
            {
                PauseDownloads(null);
                return;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"[Mega] Fail to download :{e.Message}/{url}");
                throw;
            }
            finally
            {
                try
                {
                    if (downloadPath is not null)
                        File.Delete(downloadPath);
                }
                catch (Exception cleanupException)
                {
                    Console.Error.WriteLine($"[Mega] Fail to remove temporary file:{cleanupException.Message}");
                }
                availableSlots.Release();
            }
        }
#pragma warning disable CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
        public override async Task<DownloadAddResult> Add(string url, string dir, string file_name)
        {
            if (!loginSuccessed)
            {
                Console.WriteLine($"[Mega] login fail,can't download");
                return DownloadAddResult.Failed;
            }
            if (!availableSlots.Wait(0))
                return DownloadAddResult.TryLater;
            if (Interlocked.Read(ref retryAfterTicks) > DateTime.UtcNow.Ticks)
            {
                availableSlots.Release();
                return DownloadAddResult.TryLater;
            }
            downloading.Add(DownloadTask(url, dir, file_name));
            return DownloadAddResult.Added;
        }
#pragma warning restore CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行

        private void PauseDownloads(TimeSpan? retryAfter)
        {
            var now = DateTime.UtcNow;
            var current = Interlocked.Read(ref retryAfterTicks);
            if (!retryAfter.HasValue && current > now.Ticks)
                return;
            var retryAt = now.Add(retryAfter ?? TimeSpan.FromHours(1)).Ticks;
            while (retryAt > current)
            {
                var original = Interlocked.CompareExchange(ref retryAfterTicks, retryAt, current);
                if (original == current)
                {
                    if (current <= now.Ticks)
                        Console.Error.WriteLine($"[Mega] Bandwidth limit exceeded. Retry after {new DateTime(retryAt, DateTimeKind.Utc):O}");
                    return;
                }
                current = original;
            }
        }
    }
}
