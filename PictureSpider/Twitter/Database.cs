using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Data;
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

        public async Task EnsureTwitterSchemaAsync()
        {
            // 只创建和补齐 Twitter 自己的表，避免数据库同步时影响其它模块。
            await ExecuteRawAsync(@"CREATE TABLE IF NOT EXISTS `user` (
                `id` varchar(64) NOT NULL,
                `name` varchar(128) NOT NULL DEFAULT '',
                `nick_name` varchar(300) NOT NULL DEFAULT '',
                `search_latest_tweet_id` varchar(64) NOT NULL DEFAULT '0',
                `api_latest_tweet_id` varchar(64) NOT NULL DEFAULT '0',
                `followed` tinyint(1) NOT NULL DEFAULT 0,
                `queued` tinyint(1) NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                UNIQUE KEY `IX_user_name` (`name`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

            await ExecuteRawAsync(@"CREATE TABLE IF NOT EXISTS `tweet` (
                `id` varchar(64) NOT NULL,
                `created_at` datetime(6) NOT NULL,
                `full_text` text NOT NULL,
                `user_id` varchar(64) NOT NULL DEFAULT '',
                `url` varchar(500) NOT NULL DEFAULT '',
                PRIMARY KEY (`id`),
                KEY `IX_tweet_user_id` (`user_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

            await ExecuteRawAsync(@"CREATE TABLE IF NOT EXISTS `media` (
                `id` varchar(128) NOT NULL,
                `key` varchar(128) NOT NULL DEFAULT '',
                `user_id` varchar(64) NOT NULL DEFAULT '',
                `tweet_id` varchar(64) NOT NULL DEFAULT '',
                `url` varchar(1000) NOT NULL DEFAULT '',
                `expand_url` varchar(500) NOT NULL DEFAULT '',
                `media_type` int NOT NULL DEFAULT 3,
                `file_name` varchar(260) NOT NULL DEFAULT '',
                `downloaded` tinyint(1) NOT NULL DEFAULT 0,
                `readed` tinyint(1) NOT NULL DEFAULT 0,
                `bookmarked` tinyint(1) NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                KEY `IX_media_user_id` (`user_id`),
                KEY `IX_media_tweet_id` (`tweet_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

            await ExecuteRawAsync(@"CREATE TABLE IF NOT EXISTS `auth_state` (
                `Id` varchar(64) NOT NULL,
                `Cookie` mediumtext NULL,
                `UserAgent` text NULL,
                `UpdatedAt` datetime(6) NOT NULL,
                PRIMARY KEY (`Id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

            // 兼容旧 Dapper 版本留下的表：表存在但缺列时只补最小必要列。
            await EnsureColumnAsync("user", "search_latest_tweet_id", "`search_latest_tweet_id` varchar(64) NOT NULL DEFAULT '0'");
            await EnsureColumnAsync("user", "api_latest_tweet_id", "`api_latest_tweet_id` varchar(64) NOT NULL DEFAULT '0'");
            await EnsureColumnAsync("user", "followed", "`followed` tinyint(1) NOT NULL DEFAULT 0");
            await EnsureColumnAsync("user", "queued", "`queued` tinyint(1) NOT NULL DEFAULT 0");
            await EnsureColumnAsync("media", "downloaded", "`downloaded` tinyint(1) NOT NULL DEFAULT 0");
            await EnsureColumnAsync("media", "readed", "`readed` tinyint(1) NOT NULL DEFAULT 0");
            await EnsureColumnAsync("media", "bookmarked", "`bookmarked` tinyint(1) NOT NULL DEFAULT 0");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>().ToTable("user");
            modelBuilder.Entity<User>().HasKey(x => x.id);
            modelBuilder.Entity<User>().HasIndex(x => x.name).IsUnique();
            // BaseUser 的 display 字段只服务 UI 绑定，旧表中没有对应列。
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
            var query = Users.AsQueryable();
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
            var userIds = Users.Where(user => user.queued || user.followed).Select(user => user.id);
            // 堆积媒体按较新的 tweet 优先取出，单轮数量由 Server.DownloadLimitPerRun 控制。
            var query = Medias.Where(media => !media.downloaded && userIds.Contains(media.user_id))
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

        private async Task EnsureColumnAsync(string table, string column, string definition)
        {
            var exists = await ExecuteScalarAsync($@"SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                    AND TABLE_NAME = '{table}'
                    AND COLUMN_NAME = '{column}'");
            if (exists == 0)
                await ExecuteRawAsync($"ALTER TABLE `{table}` ADD COLUMN {definition};");
        }

        private async Task ExecuteRawAsync(string sql)
        {
            var connection = Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
                await connection.OpenAsync();
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                if (shouldClose)
                    await connection.CloseAsync();
            }
        }

        private async Task<int> ExecuteScalarAsync(string sql)
        {
            var connection = Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
                await connection.OpenAsync();
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                var result = await command.ExecuteScalarAsync();
                return System.Convert.ToInt32(result);
            }
            finally
            {
                if (shouldClose)
                    await connection.CloseAsync();
            }
        }
    }
}
