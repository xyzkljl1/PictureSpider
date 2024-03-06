using HtmlAgilityPack;
using System;
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
    class ListenerServer
    {
        private string proxy;
        Pixiv.Server pixivServer =null;
        List<BaseServer> baseServers = null;
        public ListenerServer(Pixiv.Server _pServer, List<BaseServer> _baseServers, string _proxy)
        {
            pixivServer = _pServer;
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
                                if (data.Length > 0)
                                {
                                    Console.WriteLine("Receive Pixiv Cookie");
                                    await pixivServer.ListenerUtil_SetCookie(data);
                                }
                                reader.Close();
                                success = true;
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
    }
}
