using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore.Proxies;
// see BaseEFDatabase
namespace PictureSpider.Hitomi
{
    public class Database : BaseEFDatabase
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
        }
        public DbSet<Illust> Illusts { get; set; }
        public DbSet<IllustGroup> IllustGroups { get; set; }
        public DbSet<User> Users { get; set; }
    }
}
