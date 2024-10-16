using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PictureSpider
{
    static class Program
    {
        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        static void Main()
        {
            // don't dispatch exceptions to Application.ThreadException 
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);

            using (var context = new WindowsFormsSynchronizationContext())
            {
                System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                try
                {
#if DEBUG
                    AllocConsole();
#endif
                    var config = LoadConfig();
                    using (var hitomi_server = new Hitomi.Server(config))
                        using (var pixiv_server = new Pixiv.Server(config))
                            using (var lsf_server = new LocalSingleFile.Server(config))
                                using (var tg_server = new Telegram.Server(config))
                                {
                                    var common_servers = new List<BaseServer> { hitomi_server, lsf_server, tg_server };
                                    context.Post(async async => {
                                        await pixiv_server.Init();
                                        foreach(var common_server in common_servers)
                                            await common_server.Init();
                                    },null);
                                    Application.Run(new MainWindow(config, pixiv_server,common_servers));
                                }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(string.Format("捕获到未处理异常：{0}\r\n异常信息：{1}\r\n异常堆栈：{2}", ex.GetType(), ex.Message, ex.StackTrace));
                }
                finally
                {
                    System.Threading.SynchronizationContext.SetSynchronizationContext(null);
                }
            }
        }
        static private Config LoadConfig()
        {
            if (System.IO.File.Exists(@"config.json"))
            {
                using (JsonReader reader = new JsonTextReader(new System.IO.StreamReader("config.json")))
                {
                    JObject jsonObject = (JObject)JToken.ReadFrom(reader);
                    var config = new Config();
                    foreach (var fieldInfo in config.GetType().GetFields())
                        if (fieldInfo.FieldType == typeof(string))
                        {
                            if (jsonObject[fieldInfo.Name] != null
                                && jsonObject[fieldInfo.Name].Type == JTokenType.String)
                                fieldInfo.SetValue(config, jsonObject[fieldInfo.Name].ToString());
                        }
                        else if (fieldInfo.FieldType == typeof(bool))
                        {
                            if (jsonObject[fieldInfo.Name] != null
                                && jsonObject[fieldInfo.Name].Type == JTokenType.Boolean)
                                fieldInfo.SetValue(config, jsonObject[fieldInfo.Name].ToObject<Boolean>());
                        }
                        else if (fieldInfo.FieldType == typeof(int))
                        {
                            if (jsonObject[fieldInfo.Name] != null
                                && jsonObject[fieldInfo.Name].Type == JTokenType.Integer)
                                fieldInfo.SetValue(config, jsonObject[fieldInfo.Name].ToObject<int>());
                        }
                        else if (fieldInfo.FieldType == typeof(List<String>))
                        {
                            if (jsonObject[fieldInfo.Name] != null
                                && jsonObject[fieldInfo.Name].Type == JTokenType.Array)
                            {
                                var list = new List<string>();
                                foreach (var item in jsonObject[fieldInfo.Name].Value<JArray>())
                                    list.Add(item.ToString());
                                fieldInfo.SetValue(config, list);
                            }
                        }
                    return config;
                }
            }
            return new Config();
        }

    }
}
