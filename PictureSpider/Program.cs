using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PictureSpider
{
    static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
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
                    var config = LoadConfig();
                    using (var hitomi_server = new Hitomi.Server(config))
                    using(var pixiv_server = new Pixiv.Server(config))
                    {
                        context.Post(async async => {
                            await hitomi_server.Init();
                            await pixiv_server.Init();
                        },null);
                        Application.Run(new MainWindow(config, pixiv_server, hitomi_server));
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
                    return config;
                }
            }
            return new Config();
        }

    }
}
