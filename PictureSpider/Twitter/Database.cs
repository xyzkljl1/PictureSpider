using Dapper;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PictureSpider.Twitter
{
    internal class Database
    {
        private string connect_str;
        public Database(string _connect_str)
        {
            connect_str = _connect_str;
        }
        public async Task<List<User>> GetUsers(bool followed,bool queued)
        {
            if (followed && queued)
                return await StandardQuery<User>("select * from user where followed=1 or queued=1");
            else if (followed)
                return await StandardQuery<User>("select * from user where followed=1");
            else if (queued)
                return await StandardQuery<User>("select * from user where queued=1");
            return await StandardQuery<User>("select * from user");
        }
        public async Task<List<Media>> GetWaitingDownloadMedia(int limit=-1)
        {
            var sql = "SELECT media.* FROM media JOIN `user` WHERE media.user_id=`user`.id AND media.downloaded=0 AND (`user`.queued=1 OR `user`.followed=1)";
            if (limit > 0)
                sql += $" limit {limit}";
            return await StandardQuery<Media>(sql);
        }
        public async Task AddUserBase(List<User> users)
        {
            await StandardNonQuery("insert into user(`id`,`name`,`nick_name`) values(@id,@name,@nick_name) "+
                                    "on duplicate key update"+
                                    "`id`=@id,`name`=@name,`nick_name`=@nick_name", users);
        }
        public async Task UpdateUserLatestTweet(User user)
        {
            await StandardNonQuery("update user set `latest_tweet_id`=@latest_tweet_id where `id`=@id", user);
        }

        public async Task AddTweetFull(List<Tweet> tweets)
        {
            await StandardNonQuery("insert into tweet(`id`,`created_at`,`full_text`,`user_id`,`url`) values(@id,@created_at,@full_text,@user_id,@url) "+
                                    "on duplicate key update" +
                                    "`id`=@id,`created_at`=@created_at,`full_text`=@full_text,`user_id`=@user_id,`url`=@url", tweets);
        }
        public async Task AddMediaBase(List<Media> medias)
        {
            await StandardNonQuery("insert into media(`id`,`key`,`user_id`,`tweet_id`,`url`,`media_type`,`file_name`) values(@id,@key,@user_id,@tweet_id,@url,@media_type,@file_name) " +
                                    "on duplicate key update" +
                                    "`id`=@id,`key`=@key,`user_id`=@user_id,`tweet_id`=@tweet_id,`url`=@url,`media_type`=@media_type,`file_name`=@file_name", medias);
        }
        public async Task UpdateMediaDownloaded(List<Media> medias)
        {
            await StandardNonQuery("update media set `downloaded`=@downloaded where `id`=@id", medias);
        }


        public async Task StandardNonQuery<T>(String query, T objects)
        {
            try
            {
                using (IDbConnection connection = new MySqlConnection(connect_str))
                {
                    connection.Open();
                    await connection.ExecuteAsync(query, objects);
                }

            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Database Fail {0}", e.Message);
                throw;
            }
        }
        public async Task<List<T>> StandardQuery<T>(String query)
        {
            try
            {
                using (MySqlConnection connection = new MySqlConnection(this.connect_str))
                {
                    connection.Open();
                    return (await connection.QueryAsync<T>(query)).ToList();
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Database Exception {0}", e.Message);
                throw;
            }
        }
    }
}
