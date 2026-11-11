using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace PictureSpider.AuthorHub
{
    public enum SourceModule
    {
        Pixiv,
        Twitter,
        Hitomi,
        Kemono,
        Pawchive,
        LSF
    }

    public class Author
    {
        public long Id { get; set; }
        [Required, MaxLength(255)]
        public string DisplayName { get; set; } = "";
        public string StorageName { get; set; } = "";
    }

    public class AuthorSource
    {
        public SourceModule Module { get; set; }
        public string SourceKey { get; set; } = "";
        public long AuthorId { get; set; }
    }

    [PrimaryKey(nameof(AuthorId), nameof(Name))]
    public class AuthorAlias
    {
        public long AuthorId { get; set; }
        [Required, MaxLength(255)]
        public string Name { get; set; } = "";
        public bool AllowAutoMatch { get; set; }
    }

    public class MatchAuthor
    {
        public long Id { get; set; }
        [Required, MaxLength(255)]
        public string Name { get; set; } = "";
        public bool IsFallback { get; set; }
    }
}
