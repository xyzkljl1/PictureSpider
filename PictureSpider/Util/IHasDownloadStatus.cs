namespace PictureSpider
{
    public enum DownloadStatus
    {
        Pending,
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
