namespace PictureSpider.AuthorHub
{
    public enum SourceModule
    {
        Pixiv,
        Twitter,
        Hitomi,
        Kemono,
        Pawchive
    }

    public class Author
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string StorageName { get; set; } = "";
    }

    public class AuthorSource
    {
        public SourceModule Module { get; set; }
        public string SourceKey { get; set; } = "";
        public long AuthorId { get; set; }
    }
}
