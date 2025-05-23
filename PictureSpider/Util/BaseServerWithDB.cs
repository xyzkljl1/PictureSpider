﻿using Fizzler;
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
        // 要注意通过实例修改时，改的是哪个数据库的实例
        // 例如在一个函数中将实例加入downloadQueue,之后在另一个上下文中修改downloadQueue中的实例的属性，然后database.SaveChanges()是不生效的，因为实例是另一个database中的实例，需要调用另一个database的saveChanges();
        // 例如将一个对象加入downloadQueue,然后重置了DbContext，再修改该对象，即使save也不会存入数据库
        // 并且EF的数据库本身不是线程安全
        // 例如，只使用单个数据库databaseSchedule，ui上从数据库里查询到对象，然后从ui线程进行setFav,同时schedule线程也在操作，则可能令DbContext状态损坏
        protected DatabaseType databaseUI;
        protected DatabaseType databaseSchedule;// not used now
        private string ConnStr;
        virtual protected DatabaseType database
        {
            //get { return Util.IsMainThread() ? databaseUI : databaseSchedule; }
            //暂时回退
            get => databaseUI;
        }
        protected BaseServerWithDB(String connStr="")
        {
            ResetDb(connStr);
        }
        protected void ResetDb(String connStr)
        {
            ConnStr = connStr;
            databaseSchedule = new DatabaseType { ConnStr = ConnStr };
            databaseUI = new DatabaseType { ConnStr = ConnStr };
        }
        // Not used yet
        // 创建一个临时的DB，用于UI上的查询
        public virtual DatabaseType TmpDbContext()
        {
            // BaseEFDatabase会在析构时dispose
            return new DatabaseType { ConnStr=ConnStr,ReadOnly = true};
        }
    }
}
