using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PictureSpider;
using MySql.Data.MySqlClient;
using System.Data.Common;

namespace PictureSpider.Pixiv
{
    public class Database
    {
        private string connect_str;
        private Dictionary<string, TagStatus> String2TagStatus = new Dictionary<string, TagStatus> { { "Follow", TagStatus.Follow }, { "Ignore", TagStatus.Ignore }, { "None", TagStatus.None } };

        public Database(string _connect_str)
        {
            connect_str = _connect_str;
        }
        public async Task<List<int>> GetAllIllustId(string condition="")
        {
            return await StandardQuery(String.Format("select id from illust {0}",condition),
                        (DbDataReader reader) => { return reader.GetInt32(0); });
        }
        public async Task<List<int>> GetIllustIdByUpdateTime(DateTime time,float ratio=1.0f,bool reverse=false)
        {
            var list=await GetAllIllustId(String.Format("where {0}((readed=0 or bookmarked=1) and updateTime<\"{1}\")", reverse?"not":"",time.ToString("yyyy-MM-dd HH:mm:ss")));
            return new List<int>(list.Take((int)(list.Count * ratio)));
        }
        public async Task<List<Illust>> GetIllustIdAndTimeAndLikeCount()
        {
            return await StandardQuery<Illust>("select id,updateTime,likeCount from illust where readed=0 or bookmarked=1",
               (DbDataReader dataReader) => {
                   return new Illust(dataReader.GetInt32(dataReader.GetOrdinal("id")),true)
                   {
                       updateTime = Convert.ToDateTime(dataReader.GetString(dataReader.GetOrdinal("updateTime"))),
                       likeCount = dataReader.GetInt32(dataReader.GetOrdinal("likeCount")),
                   };
               });
        }
        public async Task<List<int>> GetBookmarkIllustId(bool pub)
        {
            return await StandardQuery(String.Format("select id from illust where bookmarked=true and bookmarkPrivate={0}",!pub),
                        (DbDataReader reader) => { return reader.GetInt32(0); });
        }
        public async Task<HashSet<String>> GetBannedKeyword()
        {
            return new HashSet<String>(await StandardQuery("select word from invalidkeyword",
                        (DbDataReader reader) => { return reader.GetString(0); }));
        }
        public async Task<List<int>> GetIllustIdOfQueuedOrFollowedUser()
        {
            return await GetAllIllustId("WHERE userId IN (SELECT userId FROM user WHERE followed=1 OR queued=1)");
        }
        public async Task<List<Illust>> GetAllIllustFull(string condition="")//id是int，但是可以直接GetString
        {
            return await StandardQuery<Illust>(String.Format("select * from illust {0}",condition),
               (DbDataReader dataReader) => {
                   return new Illust(dataReader.GetInt32(dataReader.GetOrdinal("id")), dataReader.GetBoolean(dataReader.GetOrdinal("valid")))
                           {
                               title = dataReader.GetString(dataReader.GetOrdinal("title")),
                               description = dataReader.GetString(dataReader.GetOrdinal("description")),
                               xRestrict = dataReader.GetInt32(dataReader.GetOrdinal("xRestrict")),
                               tags = dataReader.GetString(dataReader.GetOrdinal("tags")).Split('`').ToList(),
                               userId = dataReader.GetInt32(dataReader.GetOrdinal("userId")),
                               width = dataReader.GetInt32(dataReader.GetOrdinal("width")),
                               height = dataReader.GetInt32(dataReader.GetOrdinal("height")),
                               pageCount = dataReader.GetInt32(dataReader.GetOrdinal("pageCount")),
                               bookmarked = dataReader.GetBoolean(dataReader.GetOrdinal("bookmarked")),
                               bookmarkPrivate = dataReader.GetBoolean(dataReader.GetOrdinal("bookmarkPrivate")),
                               urlFormat = dataReader.GetString(dataReader.GetOrdinal("urlFormat")),
                               urlThumbFormat = dataReader.GetString(dataReader.GetOrdinal("urlThumbFormat")),
                               readed = dataReader.GetBoolean(dataReader.GetOrdinal("readed")),
                               bookmarkEach = dataReader.GetString(dataReader.GetOrdinal("bookmarkEach")),
                               updateTime = Convert.ToDateTime(dataReader.GetString(dataReader.GetOrdinal("updateTime"))),
                               valid = dataReader.GetBoolean(dataReader.GetOrdinal("valid")),
                               likeCount = dataReader.GetInt32(dataReader.GetOrdinal("likeCount")),
                               bookmarkCount = dataReader.GetInt32(dataReader.GetOrdinal("bookmarkCount")),
                               ugoiraURL = dataReader.GetString(dataReader.GetOrdinal("ugoiraURL")),
                               ugoiraFrames = dataReader.IsDBNull(dataReader.GetOrdinal("ugoiraFrames")) ? "" : dataReader.GetString(dataReader.GetOrdinal("ugoiraFrames"))
                   };
               });
        }
        public async Task<List<Illust>> GetIllustFullSortedByUser(int userId)//按id排序，实际等于按时间排序
        { return await GetAllIllustFull(String.Format("where `userId`={0} order by `id` DESC", userId)); }
        public async Task<List<Illust>> GetIllustFull(List<int> id_list)
        {   //要保持顺序
            var cmd=new List<String>();
            foreach (var id in id_list)
                cmd.Add(String.Format("select * from illust where id={0};", id));
            return await StandardQuery(cmd,
                        (DbDataReader dataReader) => {
                            return new Illust(dataReader.GetInt32(dataReader.GetOrdinal("id")), dataReader.GetBoolean(dataReader.GetOrdinal("valid")))
                            {
                                title = dataReader.GetString(dataReader.GetOrdinal("title")),
                                description = dataReader.GetString(dataReader.GetOrdinal("description")),
                                xRestrict = dataReader.GetInt32(dataReader.GetOrdinal("xRestrict")),
                                tags = dataReader.GetString(dataReader.GetOrdinal("tags")).Split('`').ToList(),
                                userId = dataReader.GetInt32(dataReader.GetOrdinal("userId")),
                                width = dataReader.GetInt32(dataReader.GetOrdinal("width")),
                                height = dataReader.GetInt32(dataReader.GetOrdinal("height")),
                                pageCount = dataReader.GetInt32(dataReader.GetOrdinal("pageCount")),
                                bookmarked = dataReader.GetBoolean(dataReader.GetOrdinal("bookmarked")),
                                bookmarkPrivate = dataReader.GetBoolean(dataReader.GetOrdinal("bookmarkPrivate")),
                                urlFormat = dataReader.GetString(dataReader.GetOrdinal("urlFormat")),
                                urlThumbFormat = dataReader.GetString(dataReader.GetOrdinal("urlThumbFormat")),
                                readed = dataReader.GetBoolean(dataReader.GetOrdinal("readed")),
                                bookmarkEach = dataReader.GetString(dataReader.GetOrdinal("bookmarkEach")),
                                updateTime = Convert.ToDateTime(dataReader.GetString(dataReader.GetOrdinal("updateTime"))),
                                valid = dataReader.GetBoolean(dataReader.GetOrdinal("valid")),
                                likeCount = dataReader.GetInt32(dataReader.GetOrdinal("likeCount")),
                                bookmarkCount = dataReader.GetInt32(dataReader.GetOrdinal("bookmarkCount")),
                                ugoiraURL = dataReader.GetString(dataReader.GetOrdinal("ugoiraURL")),
                                ugoiraFrames = dataReader.IsDBNull(dataReader.GetOrdinal("ugoiraFrames")) ? "" : dataReader.GetString(dataReader.GetOrdinal("ugoiraFrames"))
                            };
                        });
        }
        public async Task<List<Illust>> GetAllUnreadedIllustFull()
        {
            return await GetAllIllustFull("where `bookmarked`=0 and `readed`=0");
        }
        public async Task<string> GetCookie()
        {
            var ret = await StandardQuery<string>("select CookieCache from status where id=\"Current\"",
                (DbDataReader reader) => { return reader.GetString(0); });
            if (ret.Count == 0)
                throw new TopLevelException("there must be a row whose id is 'Current' in Table `status`");
            return ret[0];
        }
        public async Task<string> GetCSRFToken()
        {
            var ret = await StandardQuery<string>("select CSRFTokenCache from status where id=\"Current\"",
                (DbDataReader reader) => { return reader.GetString(0); });
            if (ret.Count == 0)
                throw new TopLevelException("there must be a row whose id is 'Current' in Table `status`");
            return ret[0];
        }
        public async Task<List<string>> GetFollowedTagsOrdered()
        {
            return await StandardQuery("select `word` from keyword where `status`='Follow' and type='tag' ORDER BY word ",
                        (DbDataReader reader) => { return reader.GetString(0); });
        }
        public async Task<Dictionary<string, TagStatus>> GetAllTagsStatus()
        {
            var tags = await StandardQuery("select `word`,`status` from keyword where `type`='tag'",
                        (DbDataReader reader) => { return new Tuple<string, string>(reader.GetString(0), reader.GetString(1)); });
            var ret = new Dictionary<string, TagStatus>();
            foreach (var pair in tags)
                ret[pair.Item1] = String2TagStatus[pair.Item2];
            return ret;
        }

        public async Task<Dictionary<string,string>> GetAllTagsDesc()
        {
            var tags=await StandardQuery("select `word`,`desc` from keyword where `type`='tag'",
                        (DbDataReader reader) => { return new Tuple<string,string>(reader.GetString(0),reader.GetString(1)); });
            var ret=new Dictionary<string, string>();
            foreach(var pair in tags)
                ret[pair.Item1] = pair.Item2;
            return ret;
        }
        public async Task<User> GetUserByIllustId(int illustId)
        {   //这里一定能找到user
            return (await StandardQuery(String.Format("select userId,userName,followed,queued from user where userId in (select userId from illust where id={0})", illustId),
                        (DbDataReader reader) => { return new User(reader.GetInt32(0), reader.GetString(1), reader.GetBoolean(2), reader.GetBoolean(3)); }))[0];
        }
        public async Task<int> GetQueueUpdateInterval()
        {   
            return (await StandardQuery(String.Format("SELECT datediff(NOW(),`QueueUpdateTime`) FROM status WHERE id=\"Current\";"),
                        (DbDataReader reader) => { return reader.GetInt32(0); }))[0];
        }
        public async Task<string> GetQueue()
        {   //这里一定能找到user
            return (await StandardQuery(String.Format("SELECT Queue FROM status WHERE id=\"Current\";"),
                        (DbDataReader reader) => { return reader.GetString(0); }))[0];
        }
        public async Task<User> GetUserById(int userId)
        {
            var ret=(await StandardQuery(String.Format("select userId,userName,followed,queued from user where userId ={0}", userId),
                    (DbDataReader reader) => { return new User(reader.GetInt32(0), reader.GetString(1), reader.GetBoolean(2),reader.GetBoolean(3)); }));
            if (ret != null && ret.Count > 1)
                return ret.First();
            return null;

        }
        public async Task<List<User>> GetFollowedUser(bool followed=true)
        {
            return await StandardQuery(String.Format("select userId,userName,followed,queued from user where followed={0};",followed),
                       (DbDataReader reader) => { return new User(reader.GetInt32(0), reader.GetString(1), reader.GetBoolean(2), reader.GetBoolean(3)); });
        }
        public async Task<List<User>> GetQueuedUser()
        {
            return await StandardQuery(String.Format("select userId,userName,followed,queued from user where queued=true;"),
                       (DbDataReader reader) => { return new User(reader.GetInt32(0), reader.GetString(1), reader.GetBoolean(2), reader.GetBoolean(3)); });
        }
        public async Task<List<User>> GetUnFollowedUserNeedUpdate(DateTime time)
        {
            return await StandardQuery(String.Format("select userId,userName,followed,queued from user where followed=0 and (userName=\"\" or updateTime<\"{0}\");",time.ToString("yyyy-MM-dd HH:mm:ss")),
                       (DbDataReader reader) => { return new User(reader.GetInt32(0), reader.GetString(1), reader.GetBoolean(2), reader.GetBoolean(3)); });
        }

        public async Task UpdateTagStatus(string tag, TagStatus followed)
        {
            await StandardNoneQuery("insert into keyword(`word`,`type`,`status`) values(@0,'tag',@1) on duplicate key update `status`=@1",
                (cmd) => { cmd.Parameters.AddWithValue("@0", tag);
                    if (followed == TagStatus.Follow)
                        cmd.Parameters.AddWithValue("@1", "Follow");
                    else if (followed == TagStatus.Ignore)
                        cmd.Parameters.AddWithValue("@1", "Ignore");
                    else if (followed == TagStatus.None)
                        cmd.Parameters.AddWithValue("@1", "None");
                });
        }
        public async Task UpdateIllustReaded(int id)
        {
            await StandardNoneQuery("update illust set readed=1 where id=@0", (cmd) => { cmd.Parameters.AddWithValue("@0", id); });
        }
        public async Task UpdateIllustBookmarked(int id,bool enable,bool is_private)
        {
            await StandardNoneQuery("update illust set bookmarked=@0,bookmarkPrivate=@1 where id=@2",
                (cmd) => {
                    cmd.Parameters.AddWithValue("@0", enable?1:0);
                    cmd.Parameters.AddWithValue("@1", is_private ? 1:0);
                    cmd.Parameters.AddWithValue("@2", id);
                });
        }
        public async Task UpdateIllustBookmarkEach(int id,string bookmarkEach)
        {
            await StandardNoneQuery("update illust set bookmarkEach=@0 where id=@1",
                (cmd) => {
                    cmd.Parameters.AddWithValue("@0", bookmarkEach);
                    cmd.Parameters.AddWithValue("@1", id);
                });
        }
        public async Task UpdateQueue(string queue)
        {
            await StandardNoneQuery("update status set Queue=@0,QueueUpdateTime=Now()",(cmd)=>{cmd.Parameters.AddWithValue("@0", queue); });
        }
        /*
         * 注意字段里可能有引号等,不能直接用String.Format
         */
        public void UpdateFollowedUser(List<User> data) {
            {
            using (MySqlConnection connection = new MySqlConnection(this.connect_str))
            {
                connection.Open();
                MySqlTransaction ts = null;
                try
                {
                    ts = connection.BeginTransaction();
                    int affected = 0;
                    using(var cmd = new MySqlCommand("update user set followed=false", connection, ts))
                        cmd.ExecuteNonQuery();
                    foreach (var user in data)
                    {
                        string cmdText = "insert into user values(@0,@1,@2,@3,NOW()) on duplicate key update userName=@1,followed=@2,queued=@3,updateTime=NOW();\n";
                        var cmd = new MySqlCommand(cmdText, connection, ts);
                        cmd.Parameters.AddWithValue("@0", user.userId);
                        cmd.Parameters.AddWithValue("@1", user.userName);
                        cmd.Parameters.AddWithValue("@2", user.followed);
                        cmd.Parameters.AddWithValue("@3", user.queued);
                        affected += cmd.ExecuteNonQuery();
                    }
                    ts.Commit();
                    Console.WriteLine("Affected:" + affected);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.Message);
                    ts.Rollback();
                    throw;
                }
                }
            }
        }
        public void UpdateUserName(List<User> data)
        {
            using (MySqlConnection connection = new MySqlConnection(this.connect_str))
            {
                connection.Open();
                MySqlTransaction ts = null;
                try
                {
                    ts = connection.BeginTransaction();
                    int affected = 0;
                    foreach (var user in data)
                    {
                        string cmdText = "insert user values(@0,@1,false,false,NOW()) on duplicate key update userId=@0,userName=@1,updateTime=NOW();\n";
                        var cmd = new MySqlCommand(cmdText, connection, ts);
                        cmd.Parameters.AddWithValue("@0", user.userId);
                        cmd.Parameters.AddWithValue("@1", user.userName);
                        affected += cmd.ExecuteNonQuery();
                    }
                    ts.Commit();
                    Console.WriteLine("Affected:" + affected);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.Message);
                    ts.Rollback();
                    throw;
                }
            }
        }
        public async Task UpdateUser(User user)
        {
            await StandardNoneQuery("update user set userName=@0,followed=@1,queued=@2 where userId=@3;"
                , (cmd) => {
                    cmd.Parameters.AddWithValue("@0", user.userName);
                    cmd.Parameters.AddWithValue("@1", user.followed);
                    cmd.Parameters.AddWithValue("@2", user.queued);
                    cmd.Parameters.AddWithValue("@3", user.userId);
                });
        }
        /*插入或更新illust
        注意：如果illust已经存在，readed/bookmarked/bookmarkPrivate/bookmarkEach的本地数据优先于远程数据，因此不更新
        */
        public void UpdateIllustOriginalData(List<Illust> data)
        {
            using (MySqlConnection connection = new MySqlConnection(this.connect_str))
            {
                connection.Open();
                MySqlTransaction ts = null;
                try
                {
                    ts = connection.BeginTransaction();
                    int affected = 0;
                    foreach (var illust in data)
                        if(illust.valid)
                        {
                            string cmdText = "insert ignore user(userId) values(@userId);\n" +
                                             "insert into illust values(@0,@1,@2,@3,@4,@5,@6,@7,@8,@9,@10,@11,@12,@13,@14,@15,@16,@17,NOW(),@18,@19)" +
                                             "on duplicate key update id=@0,title=@1,description=@2,xRestrict=@3,tags=@4," +
                                             "userId=@5,width=@6,height=@7,pageCount=@8," +
                                             "urlFormat=@11,urlThumbFormat=@12,valid=@15,likeCount=@16,bookmarkCount=@17,updateTime=NOW(),"+
                                             "ugoiraFrames=@18,ugoiraURL=@19;\n";
                            var cmd = new MySqlCommand(cmdText, connection, ts);
                            cmd.Parameters.AddWithValue("@userId", illust.userId);
                            cmd.Parameters.AddWithValue("@0", illust.id);
                            cmd.Parameters.AddWithValue("@1", illust.title);
                            cmd.Parameters.AddWithValue("@2", illust.description);
                            cmd.Parameters.AddWithValue("@3", illust.xRestrict);
                            cmd.Parameters.AddWithValue("@4", String.Join("`", illust.tags));
                            cmd.Parameters.AddWithValue("@5", illust.userId);
                            cmd.Parameters.AddWithValue("@6", illust.width);
                            cmd.Parameters.AddWithValue("@7", illust.height);
                            cmd.Parameters.AddWithValue("@8", illust.pageCount);
                            cmd.Parameters.AddWithValue("@9", illust.bookmarked);
                            cmd.Parameters.AddWithValue("@10", illust.bookmarkPrivate);
                            cmd.Parameters.AddWithValue("@11", illust.urlFormat);
                            cmd.Parameters.AddWithValue("@12", illust.urlThumbFormat);
                            cmd.Parameters.AddWithValue("@13", illust.readed);
                            cmd.Parameters.AddWithValue("@14", illust.bookmarkEach);
                            cmd.Parameters.AddWithValue("@15", illust.valid);
                            cmd.Parameters.AddWithValue("@16", illust.likeCount);
                            cmd.Parameters.AddWithValue("@17", illust.bookmarkCount);
                            cmd.Parameters.AddWithValue("@18", illust.ugoiraFrames);
                            cmd.Parameters.AddWithValue("@19", illust.ugoiraURL);
                            affected += cmd.ExecuteNonQuery();
                        }
                        else
                        {
                            string cmdText = "insert ignore user(userId) values(@userId);\n" +
                                             "insert into illust(id,updateTime,valid) values(@0,NOW(),@1)" +
                                             "on duplicate key update updateTime=NOW(),valid=@1;\n";
                            var cmd = new MySqlCommand(cmdText, connection, ts);
                            cmd.Parameters.AddWithValue("@userId", illust.userId);
                            cmd.Parameters.AddWithValue("@0", illust.id);
                            cmd.Parameters.AddWithValue("@1", illust.valid);
                            affected += cmd.ExecuteNonQuery();
                        }
                    ts.Commit();
                    Console.WriteLine("Affected:"+affected);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.Message);
                    ts.Rollback();
                    throw;
                }
            }
        }
        public void UpdateIllustMyData(List<Illust> data)
        {
            using (MySqlConnection connection = new MySqlConnection(this.connect_str))
            {
                connection.Open();
                MySqlTransaction ts = null;
                try
                {
                    ts = connection.BeginTransaction();
                    int affected = 0;
                    foreach (var illust in data)
                    {
                        string cmdText = "update illust set readed=@13,bookmarkEach=@14,updateTime=NOW() where id=@0;\n";
                        var cmd = new MySqlCommand(cmdText, connection, ts);
                        cmd.Parameters.AddWithValue("@0", illust.id);
                        cmd.Parameters.AddWithValue("@13", illust.readed);
                        cmd.Parameters.AddWithValue("@14", illust.bookmarkEach);
                        affected += cmd.ExecuteNonQuery();
                    }
                    ts.Commit();
                    Console.WriteLine("Affected:" + affected);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.Message);
                    ts.Rollback();
                    throw;
                }
            }
        }
        public async Task UpdateCookie(string cookie)
        {
            await StandardNoneQuery("update status set CookieCache=@0 where id=\"Current\";", (cmd) => { cmd.Parameters.AddWithValue("@0", cookie); });
        }
        public async Task UpdateCSRFToken(string token)
        {
            await StandardNoneQuery("update status set CSRFTokenCache=@0 where id=\"Current\";", (cmd) => { cmd.Parameters.AddWithValue("@0", token); });
        }

        //注意字符串必须以cmd.Parameters.AddWithValue以避免转义问题
        public async Task<int> StandardNoneQuery(String cmd_text, Action<MySqlCommand> add_para)
        {
            try
            {
                using (MySqlConnection connection = new MySqlConnection(this.connect_str))
                {
                    connection.Open();
                    var cmd = new MySqlCommand(cmd_text, connection);
                    add_para(cmd);
                    int ret=await cmd.ExecuteNonQueryAsync();
                    Console.WriteLine("Update {0} Rows",ret);
                    return ret;
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Database Exception1 {0}", e.Message);
                throw;
            }
        }
        public async Task<List<T>> StandardQuery<T>(String cmd_text,Func<DbDataReader, T> converter)
        {
            try
            {
                using (MySqlConnection connection = new MySqlConnection(this.connect_str))
                {
                    connection.Open();
                    var ret = new List<T>();
                    var cmd = new MySqlCommand(cmd_text, connection);
                    using (var dataReader = await cmd.ExecuteReaderAsync())
                        while (dataReader.Read())
                            ret.Add(converter(dataReader));
//                    Console.WriteLine("Select {0} Rows", ret.Count);
                    return ret;
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Database Exception2 {0}",e.Message);
                throw;
            }
        }
        public async Task<List<T>> StandardQuery<T>(List<String> cmd_text_list, Func<DbDataReader, T> converter)
        {
            try
            {
                using (MySqlConnection connection = new MySqlConnection(this.connect_str))
                {
                    connection.Open();
                    var ret = new List<T>();
                    var cmd = "";
                    foreach(var cmd_text in cmd_text_list)
                    {
                        cmd += cmd_text;
                        if (cmd.Length > 30000)
                        {
                            using (var dataReader = await (new MySqlCommand(cmd, connection)).ExecuteReaderAsync())
                                do
                                    while (dataReader.Read())
                                        ret.Add(converter(dataReader));
                                while (dataReader.NextResult());
                            cmd = "";
                        }
                    }
                    if(cmd.Length>0)
                        using (var dataReader = await (new MySqlCommand(cmd, connection)).ExecuteReaderAsync())
                            do
                                while (dataReader.Read())
                                    ret.Add(converter(dataReader));
                            while (dataReader.NextResult());

                    Console.WriteLine("Select {0} Rows", ret.Count);
                    return ret;
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Database Exception3 {0}", e.Message);
                throw;
            }
        }
    }
}
