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
        public List<Illust> GetIllustUnreaded()
        {
            using (MySqlConnection connection = new MySqlConnection(this.connect_str))
            {

            }
        }
        public void UpdateIllustLeft(List<Illust> data)
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
                        string cmdText = "insert ignore user(userId) values(@userId);\n" +
                                         "insert into illust values(@0,@1,@2,@3,@4,@5,@6,@7,@8,@9,@10,@11,@12,@13,@14,NOW())" +
                                         "on duplicate key update id=@0,title=@1,description=@2,xRestrict=@3,tags=@4," +
                                         "userId=@5,width=@6,height=@7,pageCount=@8,bookmarked=@9,bookmarkPrivate=@10," +
                                         "urlFormat=@11,urlThumbFormat=@12,updateTime=NOW();\n";
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
        public void UpdateIllusRight(List<Illust> data)
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
