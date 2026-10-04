using Microsoft.EntityFrameworkCore;

namespace PictureSpider.AuthorHub
{
    public class Database : BaseEFDatabase
    {
        public DbSet<Author> Authors { get; set; }
        public DbSet<AuthorSource> AuthorSources { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.HasCharSet("utf8mb4");
            modelBuilder.UseCollation("utf8mb4_0900_bin");

            modelBuilder.Entity<Author>().ToTable("Authors");
            modelBuilder.Entity<Author>().HasKey(x => x.Id);
            modelBuilder.Entity<Author>().Property(x => x.Name).IsRequired().HasMaxLength(255);

            modelBuilder.Entity<AuthorSource>().ToTable("AuthorSources");
            modelBuilder.Entity<AuthorSource>().HasKey(x => new { x.Module, x.SourceKey });
            modelBuilder.Entity<AuthorSource>().Property(x => x.Module)
                .HasConversion<string>()
                .HasColumnType("enum('Pixiv','Twitter','Hitomi','Kemono','Pawchive')");
            modelBuilder.Entity<AuthorSource>().Property(x => x.SourceKey)
                .HasMaxLength(255)
                .UseCollation("utf8mb4_0900_bin");
            // 来源映射由应用检查；AuthorId 按要求不建立外键。
            modelBuilder.Entity<AuthorSource>().HasIndex(x => x.AuthorId);
        }
    }
}
