using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PictureSpider.Twitter
{
    public class Database : BaseEFDatabase
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Tweet> Tweets { get; set; }
        public DbSet<Media> Medias { get; set; }
        public DbSet<AuthState> AuthStates { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>().ToTable("user");
            modelBuilder.Entity<User>().HasKey(x => x.id);
            modelBuilder.Entity<User>().HasIndex(x => x.name).IsUnique();
            // BaseUser display fields are UI-only and are not stored in the Twitter tables.
            modelBuilder.Entity<User>().Ignore(x => x.displayId);
            modelBuilder.Entity<User>().Ignore(x => x.displayText);

            modelBuilder.Entity<Tweet>().ToTable("tweet");
            modelBuilder.Entity<Tweet>().HasKey(x => x.id);
            modelBuilder.Entity<Tweet>().HasIndex(x => x.user_id);

            modelBuilder.Entity<Media>().ToTable("media");
            modelBuilder.Entity<Media>().HasKey(x => x.id);
            modelBuilder.Entity<Media>().HasIndex(x => x.user_id);
            modelBuilder.Entity<Media>().HasIndex(x => x.tweet_id);

            modelBuilder.Entity<AuthState>().ToTable("auth_state");
            modelBuilder.Entity<AuthState>().HasKey(x => x.Id);
        }

        public async Task<List<User>> GetUsers(bool followed, bool queued)
        {
            var query = Users.Where(user => !user.invalid);
            if (followed && queued)
                query = query.Where(user => user.followed || user.queued);
            else if (followed)
                query = query.Where(user => user.followed);
            else if (queued)
                query = query.Where(user => user.queued);
            return await query.ToListAsync();
        }

        public async Task<User> GetUserById(string user_id)
        {
            return await Users.FirstOrDefaultAsync(user => user.id == user_id);
        }

        public async Task<List<Media>> GetWaitingDownloadMedia(int limit = -1)
        {
            var userIds = Users.Where(user => !user.invalid && (user.queued || user.followed)).Select(user => user.id);
            // Drain accumulated media in newest-tweet-first batches controlled by Server.DownloadLimitPerRun.
            var query = Medias.Where(media => !media.downloaded
                                              && !media.download_unavailable
                                              && userIds.Contains(media.user_id))
                              .OrderByDescending(media => media.tweet_id);
            if (limit > 0)
                return await query.Take(limit).ToListAsync();
            return await query.ToListAsync();
        }

        public async Task<List<Media>> GetFollowedUnreadMedia()
        {
            var userIds = Users.Where(user => user.followed).Select(user => user.id);
            return await Medias.Where(media => media.downloaded
                                               && userIds.Contains(media.user_id)
                                               && !media.readed
                                               && !media.bookmarked)
                               .OrderByDescending(media => media.tweet_id)
                               .ToListAsync();
        }

        public async Task<List<Media>> GetBookmarkedMedia()
        {
            return await Medias.Where(media => media.downloaded && media.bookmarked)
                               .OrderByDescending(media => media.tweet_id)
                               .ToListAsync();
        }

        public async Task<List<Media>> GetMediaByUserId(string user_id)
        {
            return await Medias.Where(media => media.user_id == user_id && media.downloaded)
                               .OrderByDescending(media => media.tweet_id)
                               .ToListAsync();
        }
    }
}
