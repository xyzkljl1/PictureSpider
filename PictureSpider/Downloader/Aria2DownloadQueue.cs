﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using static PictureSpider.Downloader;

namespace PictureSpider
{
    public class Aria2DownloadQueue:BaseDownloadQueue
    {
        private HttpClient httpClient;//不需要登陆的地方使用不带cookie的客户端，以防被网站警告
        private string aria2_rpc_secret="";
        private int port=0;
        private string proxy = "";
        private string process_name = "";
        private Process process=null;
        private string referer = "";
        private int threads = 16;
        public Aria2DownloadQueue(DownloaderPostfix postfix,string _proxy,string _referer,int _threads=16)
        {
            aria2_rpc_secret=Guid.NewGuid().ToString();
            process_name = $"aria2c_{postfix.ToString()}";
            proxy = _proxy;
            referer = _referer;
            httpClient = new HttpClient();
            threads = _threads;
        }
        public void ClearTmpFiles(string dir)
        {
            foreach (var file in Directory.GetFiles(dir, "*.aria2"))//下载临时文件
                File.Delete(file);
        }
        public async override Task<bool> Add(string url, string dir, string file_name)
        {
            //用/以避免转义
            dir = dir.Replace('\\', '/');
            file_name = file_name.Replace('\\', '/');
            try
            {
                if (string.IsNullOrEmpty(url))
                    throw new ArgumentNullException("url");
                CheckIfProcessRunning();
                string path = $"{dir}/{file_name}";
                if (File.Exists(path))
                    File.Delete(path);
                /*id必须有，值可以随便填
                 * 虽然url是数组但是并不能一次下载多个
                 * token(rpc secret)和其它参数的格式不一样
                 * 失败时RequesttAria2Async会直接抛出异常所以此处无需验证返回的json
                */
                //dir = "E:/test/2";
                var data = String.Format("{{\"jsonrpc\": \"2.0\",\"id\":\"PixivAss\",\"method\": \"aria2.addUri\"," +
                                    "\"params\": [\"token:{0}\",[\"{1}\"],{{\"dir\":\"{2}\",\"out\":\"{3}\"" +
                                    "}}]}}",
                                    aria2_rpc_secret, url, dir, file_name);
                await RequestAria2Async(data);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                return false;
            }
            return true;
        }
        public async override Task WaitForAll()
        {
            while (!await CheckIfDownloadDone()) await Task.Delay(new TimeSpan(0, 10, 0));
        }
        private void CheckIfProcessRunning()
        {
            if(process == null|| process.HasExited)
            {
                foreach (var process in System.Diagnostics.Process.GetProcessesByName(process_name))
                    process.Kill();
                //获取一个空端口
                {
                    TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
                    listener.Start();
                    this.port = ((IPEndPoint)listener.LocalEndpoint).Port;
                    listener.Stop();
                }
                {
                    process = new System.Diagnostics.Process();
                    //右斜杠和左斜杠都可以但是不能混用(不知道为什么)
                    process.StartInfo.WorkingDirectory = System.IO.Directory.GetCurrentDirectory() + @"\aria2";
                    process.StartInfo.FileName = $"{process_name}.exe";
                    process.StartInfo.RedirectStandardOutput = false;
                    process.StartInfo.UseShellExecute = true;
#if !DEBUG
                    process.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
#endif
                    //process.StartInfo.Arguments = String.Format(@"--conf-path=aria2.conf --all-proxy=""{0}"" --header=""Cookie:{1}""", download_proxy,cookie_server.cookie);
                    //process.StartInfo.Arguments = String.Format(@"--conf-path=aria2.conf --all-proxy=""{0}""", download_proxy);
                    //Pixiv:[del]不需要代理[/del]，由于迷之原因，现在需要referer和代理才能下载了，而且岛风go还不行
                    //不要带cookie，会收到警告信
                    process.StartInfo.Arguments = String.Format(@"--conf-path=aria2.conf --rpc-secret={2} --rpc-listen-port={1} --all-proxy=""{0}"" --referer={3} -x {4}",
                                                                proxy, port, aria2_rpc_secret,referer,threads);
                    process.Start();
                    Console.WriteLine($"{process_name} Restart");
                }
            }
        }
        private async Task<bool> CheckIfDownloadDone()
        {
            try
            {
                CheckIfProcessRunning();
                var data = $"{{\"jsonrpc\": \"2.0\",\"id\":\"PixivAss\",\"method\": \"aria2.getGlobalStat\",\"params\": [\"token:{aria2_rpc_secret}\"]}}";
                var ret = JsonConvert.DeserializeObject<JObject>(await RequestAria2Async(data));
                var result = ret.Value<JObject>("result");
                float speed = (result.Value<Int32>("downloadSpeed") >> 10) / 1024.0f;
                int active = result.Value<Int32>("numActive");
                int waiting = result.Value<Int32>("numWaiting");
                int done = result.Value<Int32>("numStoppedTotal");

                if(waiting == 0 && active == 0)
                {
                    Console.WriteLine($"{process_name} Done");
                    return true;
                }
                Console.WriteLine($"{process_name} Download Status:{speed}MB/s of {active}(Running)/{waiting}(Waiting)/{done}(Done) Task");
                return false;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                throw;
            }
        }
        private async Task<string> RequestAria2Async(String data)
        {
            try
            {
                using(var content= new StringContent(data))
                    using (HttpResponseMessage response = await httpClient.PostAsync($"http://127.0.0.1:{port}/jsonrpc", content))
                    {
                        if (!response.IsSuccessStatusCode)
                            throw new Exception("HTTP Not Success");
                        return await response.Content.ReadAsStringAsync();
                    }
            }
            catch (Exception e)
            {
                string msg = e.Message;//e.InnerException.InnerException.Message;
                Console.Error.WriteLine("Request Aria RPC Fail :" + msg);
                throw;
            }
        }
    }
}
