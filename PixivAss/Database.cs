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
        public void UpdateIllust(List<Illust> data,bool force)
        {
            using (MySqlConnection connection = new MySqlConnection(this.connect_str))
            {
                connection.Open();
                MySqlTransaction ts = null;
                try
                {
                    ts = connection.BeginTransaction();
                    string cmdText = "";

                    foreach (var illust in data)
                    {
                        cmdText += String.Format("insert ignore user(userId) values({0});", illust.userId);
                        cmdText += String.Format("replace into illust values(\"{0}\",\"{1}\",\"{2}\""+
                                                    ",{3},\"{4}\",\"{5}\",{6},{7},{8},{9},{10},\"{11}\",\"{12}\","+
                                                    "{13},\"{14}\",NOW());",
                                                       illust.id,illust.title,illust.description,
                                                       illust.xRestrict,String.Join("`",illust.tags),illust.userId,illust.width,illust.height,
                                                       illust.pageCount,illust.bookmarked,illust.bookmarkPrivate,
                                                       illust.urlFormat,illust.urlThumbFormat,
                                                       illust.readed,illust.bookmarkEach
                                                       );
                    }
                    var cmd = new MySqlCommand(cmdText, connection, ts);
                    int affected = cmd.ExecuteNonQuery();
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
    }
}
