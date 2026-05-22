using HtmlAgilityPack;
using System;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

/*
 * 监听特定端口，和浏览器插件通信
 * 该类不是BaseServer
 */
namespace PictureSpider
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    class ListenerServer
    {
        private string proxy;
        List<BaseServer> baseServers = null;
        public ListenerServer(List<BaseServer> _baseServers, string _proxy)
        {
            baseServers = new List<BaseServer> (_baseServers);
            proxy = _proxy;
            Task.Run(Run).Wait();
        }
        private async void Run()
        {
            using (HttpListener listerner = new HttpListener())
            {
                //通过HKEY_LOCAL_MACHINE/SYSTEM/CurrentControlSet/Services/Tcpip/Parameters/ReservedPorts项将端口设为保留
                listerner.AuthenticationSchemes = AuthenticationSchemes.Anonymous;//指定身份验证 Anonymous匿名访问
                listerner.Prefixes.Add("http://127.0.0.1:5678/");
                listerner.Start();
                Console.WriteLine("WebServer Start Successed.......");
                while (true)
                {
                    try
                    {
                        //等待请求连接
                        //没有请求则GetContext处于阻塞状态
                        bool success = false;
                        HttpListenerContext ctx = await listerner.GetContextAsync();
                        if (ctx.Request.HttpMethod.ToLower() == "get")//queued
                        {
                            var url = HttpUtility.UrlDecode(ctx.Request.Url.AbsolutePath.Substring(1));
                            foreach (var server in baseServers)
                                if (server.ListenerUtil_IsValidUrl(url))
                                    success = await server.ListenerUtil_FollowUser(url);
                        }
                        else //receive cookie
                            using (StreamReader reader = new StreamReader(ctx.Request.InputStream))
                            {
                                String data = reader.ReadToEnd();
                                success = await DispatchCookie(data);
                                reader.Close();
                            }
                        //使用Writer输出http响应代码
                        if(success)
                            using (StreamWriter writer = new StreamWriter(ctx.Response.OutputStream))
                            {
                                ctx.Response.StatusCode = 200;
                                writer.WriteLine("Success");
                                writer.Close();
                                ctx.Response.Close();
                            }
                        else
                            using (StreamWriter writer = new StreamWriter(ctx.Response.OutputStream))
                            {
                                ctx.Response.StatusCode = 510;
                                writer.WriteLine("Fail");
                                writer.Close();
                                ctx.Response.Close();
                            }

                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine(String.Format("Error While Receiving Request {0}", e.Message));
                    }
                }
            }
        }

        private async Task<bool> DispatchCookie(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
                return false;

            var site = "pixiv";
            var cookie = data;
            var userAgent = "";
            try
            {
                var json = JObject.Parse(data);
                site = json["site"]?.ToString()?.ToLowerInvariant() ?? site;
                cookie = json["cookie"]?.ToString() ?? "";
                userAgent = json["userAgent"]?.ToString() ?? "";
            }
            catch
            {
                // Legacy PixivHelper versions post a raw Pixiv cookie string.
            }

            if (string.IsNullOrWhiteSpace(cookie))
                return false;

            Console.WriteLine($"Receive {site} Cookie");
            var success = false;
            foreach (var server in baseServers)
            {
                if (site == "pixiv" && server is Pixiv.Server)
                {
                    await server.ListenerUtil_SetCookie(cookie, userAgent);
                    success = true;
                }
                else if ((site == "twitter" || site == "x") && server is Twitter.Server)
                {
                    await server.ListenerUtil_SetCookie(cookie, userAgent);
                    success = true;
                }
            }
            return success;
        }
    }
}
