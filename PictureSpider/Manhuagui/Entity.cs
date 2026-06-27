using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PictureSpider.Manhuagui
{
    public class Database : BaseEFDatabase
    {
        public DbSet<Comic> Comics { get; set; }
        public DbSet<Chapter> Chapters { get; set; }
        public DbSet<Page> Pages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Comic>().ToTable("comic");
            modelBuilder.Entity<Comic>().HasKey(x => x.Id);

            modelBuilder.Entity<Chapter>().ToTable("chapter");
            modelBuilder.Entity<Chapter>().HasKey(x => x.Id);
            modelBuilder.Entity<Chapter>().HasIndex(x => x.ComicId);
            modelBuilder.Entity<Chapter>()
                .HasOne(x => x.Comic)
                .WithMany(x => x.Chapters)
                .HasForeignKey(x => x.ComicId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Page>().ToTable("page");
            modelBuilder.Entity<Page>().HasKey(x => x.Id);
            modelBuilder.Entity<Page>().HasIndex(x => x.ChapterId);
            modelBuilder.Entity<Page>().HasIndex(x => new { x.ChapterId, x.Index }).IsUnique();
            modelBuilder.Entity<Page>()
                .HasOne(x => x.Chapter)
                .WithMany(x => x.Pages)
                .HasForeignKey(x => x.ChapterId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    [Table("comic")]
    public class Comic
    {
        [Key]
        public virtual int Id { get; set; }
        [Required]
        [MaxLength(500)]
        public virtual string Title { get; set; } = "";
        public virtual bool Enabled { get; set; } = true;
        public virtual DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public virtual DateTime? LastCheckedAt { get; set; }
        public virtual ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();
    }

    [Table("chapter")]
    public class Chapter
    {
        [Key]
        public virtual int Id { get; set; }
        public virtual int ComicId { get; set; }
        [Required]
        [MaxLength(500)]
        public virtual string Title { get; set; } = "";
        public virtual int Index { get; set; }
        public virtual int PageCount { get; set; }
        public virtual DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public virtual DateTime? LastFetchedAt { get; set; }
        public virtual Comic Comic { get; set; }
        public virtual ICollection<Page> Pages { get; set; } = new List<Page>();
    }

    [Table("page")]
    public class Page
    {
        [Key]
        public virtual long Id { get; set; }
        public virtual int ChapterId { get; set; }
        public virtual int Index { get; set; }
        [Required]
        [MaxLength(1000)]
        public virtual string ImagePath { get; set; } = "";
        [Required]
        [MaxLength(260)]
        public virtual string FileName { get; set; } = "";
        public virtual bool Downloaded { get; set; }
        public virtual DateTime? DownloadedAt { get; set; }
        [Column(TypeName = "text")]
        public virtual string LastError { get; set; } = "";
        public virtual Chapter Chapter { get; set; }
    }
}
