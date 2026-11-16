namespace PictureSpider
{
    public enum DownloadStatus
    {
        Skip,
        Downloading,
        Downloaded,
        Deleted
    }

    public interface IHasDownloadStatus
    {
        DownloadStatus downloadStatus { get; set; }
    }
}
