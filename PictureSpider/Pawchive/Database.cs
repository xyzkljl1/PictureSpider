using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
namespace PictureSpider.Pawchive
{
    public class Database : BaseBackgroundEFDatabase
    {
        private long? nextChildId;
        // 只由串行后台调用，连续分配尚未保存的子组ID。
        public async Task<string> GetNextChildId()
        {
            if (nextChildId is null)
                nextChildId = await Database.SqlQueryRaw<long>(
                    "SELECT COALESCE(MIN(CAST(id AS SIGNED)), 0) AS Value FROM WorkGroups WHERE id LIKE '-%'").SingleAsync();
            nextChildId = checked(nextChildId.Value - 1);
            return nextChildId.Value.ToString(CultureInfo.InvariantCulture);
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<User>().Property(x => x.dowloadExternalWorks)
                .HasConversion<string>()
                .HasColumnType("enum('None','DirectExternal','KeyZipMega')")
                .HasDefaultValue(User.DownloadExternalWorkType.None);
            modelBuilder.Entity<WorkGroup>()
                .HasOne(x => x.parent)
                .WithMany(x => x.children)
                .HasForeignKey("parentId", "userservice")
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
