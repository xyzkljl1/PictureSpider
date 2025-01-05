using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
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
   Add-Migration -Context PictureSpider.Kemono.Database -Args "A ConnStr=server=127.0.0.1;port=4321;UID=root;pwd=pixivAss;database=kemono;" <版本名>
   //执行迁移文件
   Update-Database -Context PictureSpider.Kemono.Database -Args "A ConnStr=server=127.0.0.1;port=4321;UID=root;pwd=pixivAss;database=kemono;"
 */
namespace PictureSpider.Kemono
{
    public class Database : DbContext
    {
        public string ConnStr="";
        public Database(string _conn_str=""): base(){ ConnStr = _conn_str; }
        public Database(DbContextOptions<Database> options):base(options){}
        protected override void OnConfiguring(DbContextOptionsBuilder builder)
        {
            if(ConnStr=="")//为了能在命令行中迁移数据库
            {
                //命令行中生成迁移文件时，不会运行本程序所以无法读取配置
                //需要在命令行传入参数，在此处获取
                var configuration = new ConfigurationBuilder().AddCommandLine(Environment.GetCommandLineArgs().ToArray()).Build();
                ConnStr = configuration.GetSection("ConnStr").Value;
                Console.WriteLine("Using: "+ConnStr);
            }
            builder.UseMySql(ConnStr, new MySqlServerVersion(new Version()));
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            //级联删除
            modelBuilder
                .Entity<Work>()
                .HasOne(e => e.workGroup)
                .WithMany(e => e.works)
                .OnDelete(DeleteBehavior.Cascade);
        }
        public DbSet<Work> Works { get; set; }
        public DbSet<WorkGroup> WorkGroups { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<ExternalWork> ExternalWorks { get; set; }
        //加载外键(导航属性)，外键对应的对象不会自动加载，只有已经在别处查询过或显示Load才会有效
        //例如User有10个illustGroup，直接查询User则illustGroups为空，如果select其中一个illustGroup则user.illustGroups获得一个成员；如果Load则user.illustGroups获得全部成员
        public void LoadFK(User obj)
        {
            Entry(obj).Collection(r => r.workGroups).Load();
        }
        public void LoadFK(WorkGroup obj)
        {
            try
            {
                Entry(obj).Collection(r => r.works).Load();
                Entry(obj).Collection(r => r.externalWorks).Load();
                Entry(obj).Reference(r => r.user).Load();
                Entry(obj).Reference(r => r.cover).Load();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database Error:{ex.Message}");
            }
        }
        public void LoadFK(Work obj)
        {
            Entry(obj).Reference(r => r.workGroup).Load();
            Entry(obj).Reference(r => r.coverGroup).Load();
        }
        public void LoadFK(ExternalWork obj)
        {
            Entry(obj).Reference(r => r.workGroup).Load();
        }
        public void LoadFK(BaseWork obj)
        {
            if ((obj as Work) is not null)
                LoadFK(obj as Work);
            else
                LoadFK(obj as ExternalWork);
        }
    }
}
