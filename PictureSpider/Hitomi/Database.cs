using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore.Proxies;
// see BaseBackgroundEFDatabase
namespace PictureSpider.Hitomi
{
    public class Database : BaseBackgroundEFDatabase
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            //级联删除
            modelBuilder
                .Entity<Illust>()
                .HasOne(e => e.illustGroup)
                .WithMany(e => e.illusts)
                .OnDelete(DeleteBehavior.Cascade);
            base.OnModelCreating(modelBuilder);
            // 跨库外键在迁移中建立，这里只映射本库字段。
            modelBuilder.Entity<User>().Property(x => x.AuthorStorageName)
                .HasMaxLength(128).HasCharSet("utf8mb4")
                .UseCollation("utf8mb4_0900_as_ci");
            modelBuilder.Entity<User>().HasIndex(x => x.AuthorStorageName);
        }
        public DbSet<Illust> Illusts { get; set; }
        public DbSet<IllustGroup> IllustGroups { get; set; }
        public DbSet<User> Users { get; set; }
    }
}
