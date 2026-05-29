using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PictureSpider
{
    public enum PendingUiOperationKind
    {
        SetReaded = 1,
        SetBookmarked = 2,
        SetPageExcluded = 3,
        SetUserFollowOrQueue = 4
    }

    public enum PendingUserFollowQueueStatus
    {
        None = 0,
        Queued = 1,
        Followed = 2
    }

    [Table("PendingUiOperations")]
    public class PendingUiOperation
    {
        [Key]
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public PendingUiOperationKind Kind { get; set; }
        [Required]
        [MaxLength(128)]
        public string TargetKey { get; set; } = "";
        public int Value { get; set; }
    }

    public class BaseBackgroundEFDatabase : BaseEFDatabase
    {
        public DbSet<PendingUiOperation> PendingUiOperations { get; set; }
    }
}
