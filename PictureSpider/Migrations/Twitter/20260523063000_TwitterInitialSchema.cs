using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PictureSpider.Migrations.Twitter
{
    [DbContext(typeof(PictureSpider.Twitter.Database))]
    [Migration("20260523063000_TwitterInitialSchema")]
    public partial class TwitterInitialSchema : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS `user` (
                `id` varchar(64) NOT NULL,
                `name` varchar(128) NOT NULL DEFAULT '',
                `nick_name` varchar(300) NOT NULL DEFAULT '',
                `search_latest_tweet_id` varchar(64) NOT NULL DEFAULT '0',
                `api_latest_tweet_id` varchar(64) NOT NULL DEFAULT '0',
                `followed` tinyint(1) NOT NULL DEFAULT 0,
                `queued` tinyint(1) NOT NULL DEFAULT 0,
                `invalid` tinyint(1) NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                UNIQUE KEY `IX_user_name` (`name`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS `tweet` (
                `id` varchar(64) NOT NULL,
                `created_at` datetime(6) NOT NULL,
                `full_text` text NOT NULL,
                `user_id` varchar(64) NOT NULL DEFAULT '',
                `url` varchar(500) NOT NULL DEFAULT '',
                PRIMARY KEY (`id`),
                KEY `IX_tweet_user_id` (`user_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS `media` (
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

            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS `auth_state` (
                `Id` varchar(64) NOT NULL,
                `Cookie` mediumtext NULL,
                `UserAgent` text NULL,
                `UpdatedAt` datetime(6) NOT NULL,
                PRIMARY KEY (`Id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");
        }
    }
}
