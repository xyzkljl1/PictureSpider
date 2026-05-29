using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PictureSpider
{
    public enum TagStatus
    {
        None,
        Follow,
        Ignore
    };
    public enum UserFollowQueueStatus
    {
        None = 0,
        Queued = 1,
        Followed = 2
    }

    public class BaseUser
    {
        [Required]
        //[DatabaseGenerated(DatabaseGeneratedOption.Identity)][DefaultValue("123")]
        //直接[DefualtValue]并没有卵用，参考https://stackoverflow.com/questions/19554050/entity-framework-6-code-first-default-value/34894274
        [MaxLength(128)]
        [DefaultValue("")]
        public string displayId { get; set; }//区分队列的id,在不同Server中意义不同
        [Required]
        [MaxLength(128)]
        [DefaultValue("")]
        public string displayText { get; set; }//显示在UI上的队列名
        [Required]
        [DefaultValue(false)]
        public bool followed { get; set; } = false;
        [Required]
        [DefaultValue(false)]
        public bool queued { get; set; } = false;
        [NotMapped]
        public UserFollowQueueStatus FollowQueueStatus
        {
            get
            {
                if (followed)
                    return UserFollowQueueStatus.Followed;
                if (queued)
                    return UserFollowQueueStatus.Queued;
                return UserFollowQueueStatus.None;
            }
            set
            {
                switch (value)
                {
                    case UserFollowQueueStatus.Followed:
                        followed = true;
                        queued = false;
                        break;
                    case UserFollowQueueStatus.Queued:
                        followed = false;
                        queued = true;
                        break;
                    case UserFollowQueueStatus.None:
                        followed = false;
                        queued = false;
                        break;
                    default:
                        throw new InvalidOperationException($"Invalid user follow queue status: {value}.");
                }
            }
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class DbKeyAttribute : Attribute
    {
    }

    public class BaseUserEx : BaseUser
    {
        [NotMapped]
        public virtual string DbKey
        {
            get
            {
                var props = GetType().GetProperties()
                    .Where(x => Attribute.IsDefined(x, typeof(DbKeyAttribute)))
                    .ToList();
                if (props.Count != 1)
                    throw new InvalidOperationException($"{GetType().FullName} must have exactly one DbKey property.");
                var value = props[0].GetValue(this)?.ToString();
                if (string.IsNullOrWhiteSpace(value))
                    throw new InvalidOperationException($"{GetType().FullName}.{props[0].Name} DbKey is empty.");
                return value;
            }
        }
    }
}
