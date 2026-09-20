using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
namespace PictureSpider.Pawchive
{
    public class Database : BaseBackgroundEFDatabase
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<User>().Property(x => x.dowloadExternalWorks)
                .HasConversion<string>()
                .HasColumnType("enum('None','DirectExternal','KeyZipMega')")
                .HasDefaultValue(User.DownloadExternalWorkType.None);
            modelBuilder.Entity<WorkGroup>()
                .HasOne(x => x.parent)
                .WithOne(x => x.child)
                .HasForeignKey<WorkGroup>("parentId", "userservice")
                .OnDelete(DeleteBehavior.Restrict);
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
    }
}
