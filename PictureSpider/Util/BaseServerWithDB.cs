using Fizzler;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PictureSpider
{
    public abstract class BaseServerWithDB<DatabaseType>: BaseServer where DatabaseType:BaseEFDatabase,new()
    {
        // 创建两个数据库，一个用于响应UI操作在图形主线程里，一个用于RunSchedule;暂时不考虑超过两个数据库
        // DbContext不是线程安全的，也不能同时有两个连接 https://stackoverflow.com/questions/44063832/what-is-the-best-practice-in-ef-core-for-using-parallel-async-calls-with-an-inje
        // 暂定：固定只从RunSchedule和UI响应函数调数据库，各自用一个Db实例; 其中UI响应函数要阻塞主线程，避免两个响应函数同时访问数据库
        // 要注意一个实例修改了数据库后，另一个实例的缓存不会自动刷新，要么调用Entry().Reload刷新，要么重新创建实例
        private DatabaseType databaseSchedule;
        private DatabaseType databaseUI;
        private string ConnStr;
        protected DatabaseType database
        {
            get { return Util.IsMainThread() ? databaseUI : databaseSchedule; }
        }
        protected BaseServerWithDB(String connStr)
        {
            ConnStr = connStr;
            databaseSchedule = new DatabaseType { ConnStr = ConnStr };
            databaseUI = new DatabaseType { ConnStr = ConnStr };
        }
        // 重新加载databaseSchedule，以确保其它实例修改的内容会被Reload
        public void ReloadScheduleDb()
        {
            databaseSchedule = new DatabaseType { ConnStr = ConnStr };
        }

        // Not used yet
        // 创建一个临时的DB，用于UI上的查询
        public DatabaseType TmpDbContext()
        {
            return new DatabaseType { ConnStr=ConnStr};
        }
    }
}
