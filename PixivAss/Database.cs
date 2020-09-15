using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PixivAss.Data;
using MySql.Data.MySqlClient;
using System.Data.Common;

namespace PixivAss
{
    class Database
    {
        private string connect_str;
        public Database(string user,string pwd,string database)
        {
            connect_str = String.Format("server=127.0.0.1;port=4321;UID={0};pwd={1};database={2};",
                user,pwd,database);
        }
        public async Task<List<string>> GetAllIllustId()
        {
            return await StandardQuery(String.Format("select id from illust "),
                        (DbDataReader reader) => { return reader.GetString(0); });
        }
        public async Task<List<string>> GetBookmarkIllustId(bool pub)
        {
            return await StandardQuery(String.Format("select id from illust where bookmarked=true and bookmarkPrivate={0}",!pub),
                        (DbDataReader reader) => { return reader.GetString(0); });
        }
        public async Task<HashSet<String>> GetBannedKeyword()
        {
            return new HashSet<String>(await StandardQuery("select word from invalidkeyword",
                        (DbDataReader reader) => { return reader.GetString(0); }));
        }
        public List<Illust> GetAllIllustFull()
        {
            try
            {
                using (MySqlConnection connection = new MySqlConnection(this.connect_str))
                {
                    var ret = new List<Illust>();
                    connection.Open();
                    var cmd = new MySqlCommand("select * from illust", connection);
                    MySqlDataReader dataReader = cmd.ExecuteReader();
                    while (dataReader.Read())
                    {
                        //Illust illust = new Illust("", true);
                        var illust = new Illust(dataReader.GetString("id"), dataReader.GetBoolean("valid"))
                        {
                            title = dataReader.GetString(dataReader.GetOrdinal("title")),
                            description = dataReader.GetString(dataReader.GetOrdinal("description")),
                            xRestrict = dataReader.GetInt32(dataReader.GetOrdinal("xRestrict")),
                            tags = dataReader.GetString(dataReader.GetOrdinal("tags")).Split('`').ToList(),
                            userId = dataReader.GetString(dataReader.GetOrdinal("userId")),
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
                            bookmarkCount = dataReader.GetInt32(dataReader.GetOrdinal("bookmarkCount"))
                        };
                        /*id=@0,title=@1,description=@2,xRestrict=@3,tags=@4," +
                                             "userId=@5,width=@6,height=@7,pageCount=@8,bookmarked=@9,bookmarkPrivate=@10," +
                                             "urlFormat=@11,urlThumbFormat=@12,valid=@15,likeCount=@16,bookmarkCount=@17,updateTime=NOW();*/
                        /*illust.id = dataReader.GetString("id");
                        illust.title = dataReader.GetString("title");
                        illust.description = dataReader.GetString("description");
                        illust.xRestrict = dataReader.GetInt32("xRestrict");
                        illust.tags = dataReader.GetString("tags").Split('`').ToList();
                        illust.userId = dataReader.GetString("userId");
                        illust.width = dataReader.GetInt32("width");
                        illust.height = dataReader.GetInt32("height");
                        illust.pageCount = dataReader.GetInt32("pageCount");
                        illust.bookmarked = dataReader.GetBoolean("bookmarked");
                        illust.bookmarkPrivate = dataReader.GetBoolean("bookmarkPrivate");
                        illust.urlFormat = dataReader.GetString("urlFormat");
                        illust.urlThumbFormat = dataReader.GetString("urlThumbFormat");
                        illust.readed = dataReader.GetBoolean("readed");
                        illust.bookmarkEach = dataReader.GetString("bookmarkEach");
                        illust.updateTime = Convert.ToDateTime(dataReader.GetString("updateTime"));
                        illust.valid = dataReader.GetBoolean("valid");
                        illust.likeCount = dataReader.GetInt32("likeCount");
                        illust.bookmarkCount = dataReader.GetInt32("bookmarkCount");*/
                        ret.Add(illust);
                    }
                    Console.WriteLine("Selected:" + ret.Count);
                    return ret;
                    }
                }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw;
            }
        }
        public async Task<List<string>> GetAllKeyword()
        {
            return await StandardQuery("select word from keyword",
                        (DbDataReader reader) => { return reader.GetString(0); });
        }
        public async Task<User> GetUserByIllustId(string illustId)
        {   //这里一定能找到user
            return (await StandardQuery(String.Format("select userId,userName,followed from user where userId in (select userId from illust where id={0})", illustId),
                        (DbDataReader reader) => { return new User(reader.GetString(0), reader.GetString(1), reader.GetBoolean(2)); }))[0];
        }
        public async Task<List<User>> GetUser(bool followed,bool unfollowed)
        {
            if (followed == false && unfollowed == false)
                return new List<User>();
            string condition = "";
            if (followed&&!unfollowed)
                condition += " where followed=true";
            else if(unfollowed && !followed)
                condition += " where followed=false";
            return await StandardQuery(String.Format("select userId,userName,followed from user{0})", unfollowed),
                       (DbDataReader reader) => { return new User(reader.GetString(0), reader.GetString(1), reader.GetBoolean(2)); });
        }
        public void UpdateFollowedUser(List<User> data) {
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
                        string cmdText = "insert into user values(@0,@1,@2,NOW()) on duplicate key update userName=@1,followed=@2,updateTime=NOW();\n";
                        var cmd = new MySqlCommand(cmdText, connection, ts);
                        cmd.Parameters.AddWithValue("@0", user.userId);
                        cmd.Parameters.AddWithValue("@1", user.userName);
                        cmd.Parameters.AddWithValue("@2", user.followed);
                        affected += cmd.ExecuteNonQuery();
                    }
                    ts.Commit();
                    Console.WriteLine("Affected:" + affected);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    ts.Rollback();
                    throw;
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
                        string cmdText = "insert user values(@0,@1,false,NOW()) on duplicate key update userId=@0,userName=@1,updateTime=NOW();\n";
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
                    Console.WriteLine(e.Message);
                    ts.Rollback();
                    throw;
                }
            }
        }
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
                                             "insert into illust values(@0,@1,@2,@3,@4,@5,@6,@7,@8,@9,@10,@11,@12,@13,@14,@15,@16,@17,NOW())" +
                                             "on duplicate key update id=@0,title=@1,description=@2,xRestrict=@3,tags=@4," +
                                             "userId=@5,width=@6,height=@7,pageCount=@8,bookmarked=@9,bookmarkPrivate=@10," +
                                             "urlFormat=@11,urlThumbFormat=@12,valid=@15,likeCount=@16,bookmarkCount=@17,updateTime=NOW();\n";
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
                    Console.WriteLine(e.Message);
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
                    Console.WriteLine(e.Message);
                    ts.Rollback();
                    throw;
                }
            }
        }
        
        public async Task<string> GetCookie()
        {
            var ret=await StandardQuery<string>("select CookieCache from status where id=\"Current\"",
                (DbDataReader reader)  =>{ return reader.GetString(0); });
            if (ret.Count == 0)
                return "";
            return ret[0];
        }
        public async Task UpdateCookie(string cookie)
        {
            var ret=await StandardNonQuery(String.Format("update status set CookieCache=\"{0}\" where id=\"Current\"",""));
            if (ret < 1)
                throw new ArgumentOutOfRangeException("Cant Update Cookie");
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
                    return ret;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Database Exception {0}",e.Message);
                throw;
            }
        }
        public async Task<int> StandardNonQuery(String cmd_text)
        {
            try
            {
                using (MySqlConnection connection = new MySqlConnection(this.connect_str))
                {
                    connection.Open();
                    var cmd = new MySqlCommand(cmd_text, connection);
                    return await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Database Exception {0}", e.Message);
                throw;
            }
        }
        public int StandardNonQuery(List<String> cmd_list)
        {
            try
            {
                using (MySqlConnection connection = new MySqlConnection(this.connect_str))
                {
                    connection.Open();
                    int affected = 0;
                    MySqlTransaction ts = null;
                    ts = connection.BeginTransaction();
                    foreach(var cmd_text in cmd_list)
                    {
                        var cmd = new MySqlCommand(cmd_text, connection);
                        affected+=cmd.ExecuteNonQuery();
                    }
                    ts.Commit();
                    return affected;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Database Exception {0}", e.Message);
                throw;
            }
        }

    }
}
