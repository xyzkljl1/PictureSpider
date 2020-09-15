using System;
using System.Net;
using System.IO;
using System.Threading.Tasks;

namespace PixivAss
{
    class CookieServer
    {
        public string cookie = "";
        private Database database;
        public CookieServer(Database _database)
        {
            database = _database;
            ReadCookie().Wait();
            Run();
        }
        private async void Run()
        {
            using (HttpListener listerner = new HttpListener())
            {
                //通过HKEY_LOCAL_MACHINE/SYSTEM/CurrentControlSet/Services/Tcpip/Parameters/ReservedPorts项将端口设为保留
                listerner.AuthenticationSchemes = AuthenticationSchemes.Anonymous;//指定身份验证 Anonymous匿名访问
                listerner.Prefixes.Add("http://127.0.0.1:56791/");
                listerner.Start();
                Console.WriteLine("WebServer Start Successed.......");
                while (true)
                {
                    //等待请求连接
                    //没有请求则GetContext处于阻塞状态
                    HttpListenerContext ctx = await listerner.GetContextAsync();
                    ctx.Response.StatusCode = 200;//设置返回给客服端http状态代码
                    string name = ctx.Request.QueryString["name"];

                    if (name != null)
                        Console.WriteLine(name);
                    using (StreamReader reader = new StreamReader(ctx.Request.InputStream))
                    {
                        String data = reader.ReadToEnd();
                        if (data.Length > 0)
                        {
                            Console.WriteLine("Receive Cookie");
                            this.cookie = data;
                            await database.UpdateCookie(cookie);
                        }
                        reader.Close();
                    }
                    //使用Writer输出http响应代码
                    using (StreamWriter writer = new StreamWriter(ctx.Response.OutputStream))
                    {
                        writer.WriteLine("Success");
                        writer.Close();
                        ctx.Response.Close();
                    }
                }
            }
        }
        private async Task ReadCookie()
        {
            cookie = await database.GetCookie();
        }
    }
}
