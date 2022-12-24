using System;
using System.Net;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using HtmlAgilityPack;


namespace PictureSpider.Pixiv
{
    class CookieServer
    {
        public string cookie = "";
        public string csrf_token = "";
        private Database database;
        private string proxy;
        public CookieServer(Database _database, string _proxy)
        {
            database = _database;
            proxy = _proxy;
            Task.Run(Init).Wait();
        }
        private async void Run()
        {
            using (HttpListener listerner = new HttpListener())
            {
                //通过HKEY_LOCAL_MACHINE/SYSTEM/CurrentControlSet/Services/Tcpip/Parameters/ReservedPorts项将端口设为保留
                listerner.AuthenticationSchemes = AuthenticationSchemes.Anonymous;//指定身份验证 Anonymous匿名访问
                listerner.Prefixes.Add("http://127.0.0.1:56781/");
                listerner.Start();
                Console.WriteLine("WebServer Start Successed.......");
                while (true)
                {
                    try
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
                                if (this.cookie != data)
                                {
                                    this.cookie = data;
                                    await database.UpdateCookie(cookie);
                                    await FetchCSRFToken();
                                }
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
                    catch (Exception e)
                    {
                        Console.Error.WriteLine(String.Format("Error While Receiving Cookie {0}",e.Message));
                    }
                }
            }
        }
        private async Task Init()
        {
            cookie = await database.GetCookie();

            csrf_token = await database.GetCSRFToken();
            if (string.IsNullOrEmpty(csrf_token) && !string.IsNullOrEmpty(cookie))
                await FetchCSRFToken();
            Run();
        }
        /*
         * CSRFToken是和cookie中的phpsessionid一一对应的token，随机生成并随登陆表单提交
         * 部分操作如收藏作品必须在请求头附带登录时所用的CSRFToken，该token会在部分网页作为不显示的元素出现
         * x-csrf-token/post_key/tt都可指代CSRFToken
         */
        private async Task FetchCSRFToken()
        {
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var handler = new HttpClientHandler()
            {
                MaxConnectionsPerServer = 256,
                UseCookies = false,
                Proxy = new WebProxy(proxy, false)
            };
            handler.ServerCertificateCustomValidationCallback = delegate { return true; };
            var httpClient = new HttpClient(handler);
            httpClient.Timeout = new TimeSpan(0, 0, 35);
            httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3");
            httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,ja;q=0.8");
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/76.0.3809.100 Safari/537.36");
            httpClient.DefaultRequestHeaders.Host = "www.pixiv.net";
            httpClient.DefaultRequestHeaders.Add("Cookie", this.cookie);
            httpClient.DefaultRequestHeaders.Add("sec-fetch-mode", "navigate");
            httpClient.DefaultRequestHeaders.Add("sec-fetch-site", "none");
            httpClient.DefaultRequestHeaders.Add("sec-fetch-user", "?1");
            //id为1的作品的编辑收藏页面，这个作品存不存在/是否已加入收藏不影响，设置语言表单里会带token
            var url = "https://www.pixiv.net/bookmark_add.php?type=illust&illust_id=1";
            this.csrf_token = "";
            for (int try_ct = 5; try_ct >=0;--try_ct)
                try
                {
                    using (HttpResponseMessage response = await httpClient.GetAsync(url))
                    //if(response.StatusCode==HttpStatusCode.OK)
                    {
                        var ret = await response.Content.ReadAsStringAsync();
                        var doc = new HtmlDocument();
                        doc.LoadHtml(ret);
                        HtmlNode headNode = doc.DocumentNode.SelectSingleNode("//input[@name='tt']");
                        if (headNode != null)
                        {
                            this.csrf_token = headNode.Attributes["value"].Value;
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message + "Re Try " + try_ct.ToString() + " On :" + url);
                    if (try_ct == 0)
                        throw;
                }
            await database.UpdateCSRFToken(csrf_token);
        }
    }
}
