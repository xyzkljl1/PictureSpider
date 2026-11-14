using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
namespace PictureSpider.Kemono
{
    public class Database : BaseBackgroundEFDatabase
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // 保持已有待执行操作表的时间精度，避免迁移时修改无关列。
            modelBuilder.Entity<PendingUiOperation>().Property(x => x.CreatedAt).HasColumnType("datetime(6)");
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
