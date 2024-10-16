using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
/*
 * 使用EntityFramework框架
   注意应当在nuget中安装Microsoft.EntityFrameworkCore，而非旧版.net使用的EntityFramework
   为了使用控制台工具还需要Microsoft.EntityFrameworkCore.Tools
  
 * 数据库迁移：
   打开nuget控制台(默认项目选当前项目)执行命令
   //仅初次需要,不确定是否需要-Context
   Enable-Migrations
   //在Migrations下生成迁移文件
   //有多个dbContext时必须使用-Context指定,context为类名，如-Context PictureSpider.LocalSingleFile.Database
   //因为此时并不会运行程序无法获得参数，需要从命令行传入-Args；其中A可以是任意字符，用来占位使得configuration派生参数时key和value不会错位
   //似乎可以忽略的警报？：An error occurred while accessing the Microsoft.Extensions.Hosting services. Continuing without the application service provider. Error: The entry point exited without ever building an IHost.
   Add-Migration -Context PictureSpider.Telegram.Database -Args "A ConnStr=server=127.0.0.1;port=4321;UID=root;pwd=pixivAss;database=telegram;" <版本名>
   //执行迁移文件
   Update-Database -Context PictureSpider.Telegram.Database -Args "A ConnStr=server=127.0.0.1;port=4321;UID=root;pwd=pixivAss;database=telegram;"

   自动生成的迁移文件内容受DatabaseModelSnapshot影响，如果迁移出现问题，尝试修改DatabaseModelSnapshot
 */
namespace PictureSpider.Telegram
{
    public class Database : DbContext
    {
        public string ConnStr = "";
        public Database(string _conn_str = "") : base() { ConnStr = _conn_str; }
        public Database(DbContextOptions<Database> options) : base(options) { }
        protected override void OnConfiguring(DbContextOptionsBuilder builder)
        {
            if (ConnStr == "")//为了能在命令行中迁移数据库
            {
                //命令行中生成迁移文件时，不会运行本程序所以无法读取配置
                //需要在命令行传入参数，在此处获取
                var configuration = new ConfigurationBuilder().AddCommandLine(Environment.GetCommandLineArgs().ToArray()).Build();
                ConnStr = configuration.GetSection("ConnStr").Value;
                Console.WriteLine("Using: " + ConnStr);
            }
            builder.UseMySql(ConnStr, new MySqlServerVersion(new Version()));
        }
        public bool AddOrIgnoreMessage(TdLib.TdApi.Message messageInfo)
        {
            var message = Messages.FirstOrDefault(ele => ele.id == messageInfo.Id&&ele.chat==messageInfo.ChatId);
            if (message is null)
            {
                message = new Message { id = messageInfo.Id, chat = messageInfo.ChatId, timestamp = messageInfo.Date,albumid=messageInfo.MediaAlbumId, json = JsonConvert.SerializeObject(messageInfo) };
                Messages.Add(message);
                return true;
            }
            return false;
        }
        public DbSet<Channel> Channels { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<FinishedTask> FinishedTasks { get; set; }
    }
}
