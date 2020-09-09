using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PixivAss.Data;
using MySql.Data;
using MySql.Data.MySqlClient;

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
        public List<string> GetAllIllustId()
        {
            using (MySqlConnection connection = new MySqlConnection(this.connect_str))
            {
                try
                {
                    var ret = new List<string>();
                    connection.Open();
                    var cmd = new MySqlCommand("select id from illust", connection);
                    MySqlDataReader dataReader = cmd.ExecuteReader();
                    while (dataReader.Read())
                        ret.Add(dataReader.GetString("id"));
                    Console.WriteLine("Selected:" + ret.Count);
                    connection.Close();
                    return ret;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    throw;
                }
            }
        }

        public List<string> GetBookmarkIllustId(bool pub)
        {
            using (MySqlConnection connection = new MySqlConnection(this.connect_str))
            {
                try
                {
                    var ret =new List<string>();
                    connection.Open();
                    var cmd = new MySqlCommand("select id from illust where bookmarked=true and bookmarkPrivate="+(pub?"false":"true"), connection);
                    MySqlDataReader dataReader = cmd.ExecuteReader();
                    while (dataReader.Read())
                        ret.Add(dataReader.GetString("id"));
                    Console.WriteLine("Selected:" + ret.Count);
                    connection.Close();
                    return ret;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    throw;
                }
            }
        }
        public List<Illust> GetAllIllustFull(bool pub)
        {
            using (MySqlConnection connection = new MySqlConnection(this.connect_str))
            {
                try
                {
                    var ret = new List<Illust>();
                    connection.Open();
                    var cmd = new MySqlCommand("select * from illust", connection);
                    MySqlDataReader dataReader = cmd.ExecuteReader();
                    while (dataReader.Read())
                    {
                        Illust illust = new Illust("",true);
                        /*id=@0,title=@1,description=@2,xRestrict=@3,tags=@4," +
                                             "userId=@5,width=@6,height=@7,pageCount=@8,bookmarked=@9,bookmarkPrivate=@10," +
                                             "urlFormat=@11,urlThumbFormat=@12,updateTime=NOW(),valid=@15;*/
                        illust.id=dataReader.GetString("id");
                        illust.title=dataReader.GetString("title");
                        illust.description=dataReader.GetString("description");
                        illust.xRestrict=dataReader.GetInt32("xRestrict");
                        illust.tags=dataReader.GetString("tags").Split('`').ToList();
                        illust.userId=dataReader.GetString("userId");
                        illust.width=dataReader.GetInt32("width");
                        illust.height=dataReader.GetInt32("height");
                        illust.pageCount=dataReader.GetInt32("pageCount");
                        illust.bookmarked=dataReader.GetBoolean("bookmarked");
                        illust.bookmarkPrivate=dataReader.GetBoolean("bookmarkPrivate");
                        illust.urlFormat=dataReader.GetString("urlFormat");
                        illust.urlThumbFormat=dataReader.GetString("urlThumbFormat");
                        illust.readed=dataReader.GetBoolean("readed");
                        illust.bookmarkEach=dataReader.GetString("bookmarkEach");
                        illust.updateTime =Convert.ToDateTime(dataReader.GetString("updateTime"));
                        illust.valid=dataReader.GetBoolean("valid");
                        ret.Add(illust);
                    }
                    Console.WriteLine("Selected:" + ret.Count);
                    connection.Close();
                    return ret;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    throw;
                }
            }
        }

        public User GetUserByIllustId(string illustId)
        {
            using (MySqlConnection connection = new MySqlConnection(this.connect_str))
            {
                try
                {
                    User ret = null;
                    connection.Open();
                    var cmd = new MySqlCommand("select * from user where userId in (select userId from illust where id=@0)", connection);
                    cmd.Parameters.AddWithValue("@0", illustId);
                    MySqlDataReader dataReader = cmd.ExecuteReader();
                    if (dataReader.Read())
                        ret = new User(dataReader.GetString("userId"), dataReader.GetString("userName"), dataReader.GetBoolean("followed"));
                    connection.Close();
                    return ret;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    throw;
                }
            }
        }
        public List<User> GetUser(bool followed,bool unfollowed)
        {
            if (followed == false && unfollowed == false)
                return new List<User>();
            using (MySqlConnection connection = new MySqlConnection(this.connect_str))
            {
                try
                {
                    connection.Open();
                    var ret = new List<User>();
                    string cmdText = "select * from user";
                    if (followed == true && unfollowed == true)
                        ;
                    else if (followed == true)
                        cmdText += " where followed=true";
                    else
                        cmdText += " where followed=false";
                    var cmd = new MySqlCommand(cmdText, connection);
                    MySqlDataReader dataReader = cmd.ExecuteReader();
                    while(dataReader.Read())
                        ret.Add( new User(dataReader.GetString("userId"),dataReader.GetString("userName"),dataReader.GetBoolean("followed")));
                    Console.WriteLine("Selected:" + ret.Count);
                    connection.Close();
                    return ret;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    throw;
                }
            }
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
                    {
                    var cmd = new MySqlCommand("update user set followed=false", connection, ts);
                    cmd.ExecuteNonQuery();
                    }
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
                connection.Close();
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
                connection.Close();
            }
        }
        public void UpdateIllustAllCol(List<Illust> data)
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
                                             "insert into illust values(@0,@1,@2,@3,@4,@5,@6,@7,@8,@9,@10,@11,@12,@13,@14,NOW(),@15)" +
                                             "on duplicate key update id=@0,title=@1,description=@2,xRestrict=@3,tags=@4," +
                                             "userId=@5,width=@6,height=@7,pageCount=@8,bookmarked=@9,bookmarkPrivate=@10," +
                                             "urlFormat=@11,urlThumbFormat=@12,updateTime=NOW(),valid=@15;\n";
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
                connection.Close();
            }
        }
        public void UpdateIllustRight(List<Illust> data)
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
                connection.Close();
            }
        }
    }
}
