using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                    Util.SetMainThreadId();
                    using (var kemonoServer = new Kemono.Server(config))
                    using (var hitomiServer = new Hitomi.Server(config))
                        using (var pixivServer = new Pixiv.Server(config))
                            using (var lsfServer = new LocalSingleFile.Server(config))
                                using (var tgServer = new Telegram.Server(config))
                                {
                                    var commonServers = new List<BaseServer> { hitomiServer, lsfServer, tgServer, kemonoServer };
                                    context.Post(async async => {
                                        await pixivServer.Init();
                                        foreach(var commonServer in commonServers)
                                            await commonServer.Init();
                                    },null);
                                    Application.Run(new MainWindow(config, pixivServer,commonServers));
                                }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"捕获到未处理异常：{ex.GetType()}\r\n异常信息：{ex.Message}\r\n异常堆栈：{ex.StackTrace}");
                }
                finally
                {
                    System.Threading.SynchronizationContext.SetSynchronizationContext(null);
                }
            }
        }
        private static Config LoadConfig()
        {
            if (System.IO.File.Exists(@"config.json"))
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
            return new Config();
        }

    }
}
