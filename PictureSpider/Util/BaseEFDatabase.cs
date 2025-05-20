using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/*
 * 使用EntityFramework框架
   注意应当在nuget中安装Microsoft.EntityFrameworkCore，而非旧版.net使用的EntityFramework
   为了使用控制台工具还需要Microsoft.EntityFrameworkCore.Tools
  
 * 数据库迁移：
   打开nuget控制台(默认项目选当前项目)执行命令
   //仅初次需要
   Enable-Migrations  
   //在Migrations下生成迁移文件
   //有多个dbContext时必须使用-Context指定,context为类名，如-Context PictureSpider.Hitomi.Database
   //因为此时并不会运行程序无法获得参数，需要从命令行传入-Args；其中A可以是任意字符，用来占位使得configuration派生参数时key和value不会错位
   //似乎可以忽略的警报？：An error occurred while accessing the Microsoft.Extensions.Hosting services. Continuing without the application service provider. Error: The entry point exited without ever building an IHost.
   Add-Migration -Context PictureSpider.Hitomi.Database -Args "A ConnStr=server=127.0.0.1;port=4321;UID=root;pwd=pixivAss;database=hitomi;" <版本名>
   //执行迁移文件
   Update-Database -Context PictureSpider.Hitomi.Database -Args "A ConnStr=server=127.0.0.1;port=4321;UID=root;pwd=pixivAss;database=hitomi;"

 * 如果migration实在对不上，先手动统一数据库表结构，add-migration一次，删掉migration文件
 * 或者删掉Migrations\DatabaseMigrations\DatabaseModelSnapshot.cs
 * 
 */
namespace PictureSpider
{
    public class BaseEFDatabase : DbContext
    {
        public string ConnStr = "";
        public bool ReadOnly = false;
        // 构造: new BaseEFDatabase{ ConnStr = ""}; 不用public BaseEFDatabase(str _connStr)是为了避免在每个子类中都要重复定义一个一样的构造函数
        public BaseEFDatabase() { }
        // Always Dispose on destructor 
        ~BaseEFDatabase() { base.Dispose(); }
        public override void Dispose() {
            // 不应该调用该函数
            // 但是命令行迁移数据库时会掉，不能throw new NotImplementedException();
        }
        private BaseEFDatabase(String _connStr) { throw new NotImplementedException(); }
        protected override void OnConfiguring(DbContextOptionsBuilder builder)
        {
            // 开启lazyload外键
            builder.UseLazyLoadingProxies();
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
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            /*
            // EagerLoad查询时自动加载外键 https://learn.microsoft.com/zh-cn/ef/core/querying/related-data/eager
            // 目前使用基于proxies的lazyload https://learn.microsoft.com/zh-cn/ef/core/querying/related-data/lazy
            modelBuilder.Entity<IllustGroup>().Navigation(e => e.user).AutoInclude();
            modelBuilder.Entity<IllustGroup>().Navigation(e => e.illusts).AutoInclude();
            modelBuilder.Entity<User>().Navigation(e => e.illustGroups).AutoInclude();
            modelBuilder.Entity<Illust>().Navigation(e => e.illustGroup).AutoInclude();*/
        }
        public override int SaveChanges()
        {
            if (ReadOnly)
                throw new NotImplementedException("ReadOnly DB!");
            return base.SaveChanges();
        }
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ReadOnly)
                throw new NotImplementedException("ReadOnly DB!");
            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
