using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PictureSpider.Pixiv
{
    public class Database : BaseBackgroundEFDatabase
    {
        public DbSet<Illust> Illusts { get; set; }
        public DbSet<User> Users { get; set; }
        public Database() { }
        public Database(string connectStr) { ConnStr = connectStr; }

        protected override void OnConfiguring(DbContextOptionsBuilder builder)
        {
            builder.UseMySql(ConnStr, new MySqlServerVersion(new Version(8, 0, 31)));
            builder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PendingUiOperation>().Property(x => x.Kind).HasConversion<string>()
                .HasColumnType("enum('SetReaded','SetBookmarked','SetPageExcluded','SetUserFollowOrQueue','AddQueuedUser','SetTagStatus','SetLoginInfo')");
            modelBuilder.Entity<PendingUiOperation>().Property(x => x.Cookie).HasColumnType("text").IsRequired(false);
            modelBuilder.Entity<PendingUiOperation>().Property(x => x.UserAgent).HasColumnType("text").IsRequired(false);
            var illust = modelBuilder.Entity<Illust>();
            illust.ToTable("illust");
            foreach (var field in typeof(Illust).GetFields().Where(x => x.Name != "userName" && x.Name != "score" && x.Name != "debugMsg"))
                illust.Property(field.FieldType, field.Name);
            illust.HasKey(nameof(Illust.id));
            illust.Property(x => x.id).ValueGeneratedNever();
            illust.Property(x => x.tags).HasConversion(
                value => string.Join("`", value),
                value => value.Split('`', StringSplitOptions.None).ToList())
                .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                    (left, right) => left.SequenceEqual(right),
                    value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                    value => value.ToList()));
            // 保持原 MySql.Data 的非零布尔值、整数溢出和 TIMESTAMP 本地时间语义。
            illust.Property(x => x.valid).HasConversion(value => value ? 1 : 0, value => value != 0).HasColumnType("int");
            illust.Property(x => x.width).HasConversion(value => checked((uint)value), value => checked((int)value)).HasColumnType("int unsigned");
            illust.Property(x => x.height).HasConversion(value => checked((uint)value), value => checked((int)value)).HasColumnType("int unsigned");
            illust.Property(x => x.pageCount).HasConversion(value => checked((uint)value), value => checked((int)value)).HasColumnType("int unsigned");
            illust.Property(x => x.updateTime).HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Local)).HasColumnType("timestamp");
            illust.Property(x => x.uploadDate).HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Local)).HasColumnType("timestamp");
            illust.Property(x => x.ugoiraFrames).IsRequired(false);

            var user = modelBuilder.Entity<User>();
            user.ToTable("user");
            user.Ignore(x => x.displayId);
            user.Ignore(x => x.displayText);
            foreach (var field in typeof(User).GetFields())
                user.Property(field.FieldType, field.Name);
            user.HasKey(nameof(User.userId));
            user.Property(x => x.userId).ValueGeneratedNever();
            user.Property<DateTime>("updateTime").HasColumnType("timestamp");
            user.Property(x => x.AuthorStorageName).HasMaxLength(128).IsRequired(false);
        }
        public Task<List<int>> GetAllIllustId(string condition = "")
        {
            var sql = $"select id from illust {condition}";
            return base.Database.SqlQueryRaw<int>(sql).ToListAsync();
        }
        public async Task<List<int>> GetIllustIdByUpdateTime(DateTime time, float ratio = 1.0f, bool reverse = false)
        {
            var list = await GetAllIllustId(string.Format("where {0}((readed=0 or bookmarked=1) and updateTime<\"{1}\")", reverse ? "not" : "", time.ToString("yyyy-MM-dd HH:mm:ss")));
            var ct = await Illusts.CountAsync();
            return list.Take((int)(ct * ratio)).ToList();
        }
        public Task<List<int>> GetBookmarkIllustId(bool pub)
        {
            return Illusts.Where(x => x.bookmarked && x.bookmarkPrivate == !pub).Select(x => x.id).ToListAsync();
        }
        public async Task<HashSet<string>> GetBannedKeyword()
        {
            return (await base.Database.SqlQueryRaw<string>("select word from invalidkeyword").ToListAsync()).ToHashSet();
        }
        public async Task<List<Illust>> GetIllustFullSortedByUser(int userId)
        {
            var result = await Illusts.Where(x => x.userId == userId).OrderByDescending(x => x.id).ToListAsync();
            foreach (var illust in result)
                illust.ugoiraFrames ??= "";
            return result;
        }
        public async Task<List<Illust>> GetIllustFull(List<int> id_list)
        {
            var result = new List<Illust>();
            // UNION ALL 保留重复 ID；按输入位置排序，不依赖数据库的默认返回顺序。
            for (int offset = 0; offset < id_list.Count; offset += 500)
            {
                var ids = id_list.Skip(offset).Take(500).ToList();
                var positions = string.Join(" UNION ALL ", ids.Select((id, index) => $"SELECT {id} AS id, {index} AS position"));
                var sql = $"SELECT illust.* FROM ({positions}) AS requested INNER JOIN illust ON illust.id=requested.id ORDER BY requested.position";
                var batch = await Illusts.FromSqlRaw(sql).ToListAsync();
                foreach (var illust in batch)
                    illust.ugoiraFrames ??= "";
                result.AddRange(batch);
            }
            return result;
        }
        public async Task<List<Illust>> GetAllUnreadedIllustFull()
        {
            var result = await Illusts.Where(x => !x.bookmarked && !x.readed).ToListAsync();
            foreach (var illust in result)
                illust.ugoiraFrames ??= "";
            return result;
        }
        public async Task<string> GetCookie()
        {
            var result = await base.Database.SqlQueryRaw<string>("select CookieCache from status where id='Current'").ToListAsync();
            if (result.Count == 0)
                throw new TopLevelException("there must be a row whose id is 'Current' in Table `status`");
            return result[0];
        }
        public async Task<string> GetCSRFToken()
        {
            var result = await base.Database.SqlQueryRaw<string>("select CSRFTokenCache from status where id='Current'").ToListAsync();
            if (result.Count == 0)
                throw new TopLevelException("there must be a row whose id is 'Current' in Table `status`");
            return result[0];
        }
        public async Task EnsureUserAgentCache()
        {
            var exists = await base.Database.SqlQueryRaw<int>("SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='status' AND COLUMN_NAME='UserAgentCache'").ToListAsync();
            if (exists.Count == 0 || exists[0] == 0)
                await StandardNoneQuery("ALTER TABLE `status` ADD COLUMN UserAgentCache text COLLATE utf8mb3_bin NULL", cmd => { });
        }
        public async Task<string> GetUserAgent(string fallback)
        {
            var result = await base.Database.SqlQueryRaw<string>("select UserAgentCache from status where id='Current'").ToListAsync();
            if (result.Count == 0)
                throw new TopLevelException("there must be a row whose id is 'Current' in Table `status`");
            return string.IsNullOrWhiteSpace(result[0]) ? fallback : result[0];
        }
        public Task<List<string>> GetFollowedTagsOrdered()
        {
            return base.Database.SqlQueryRaw<string>("select word from keyword where status='Follow' and type='tag' ORDER BY word").ToListAsync();
        }
        public Dictionary<string, TagStatus> GetAllTagsStatusSync()
        {
            return base.Database.SqlQueryRaw<TagRow>("select word,status,`desc` from keyword where type='tag'").ToList()
                .ToDictionary(x => x.word, x => Enum.Parse<TagStatus>(x.status));
        }
        public Dictionary<string, string> GetAllTagsDescSync()
        {
            return base.Database.SqlQueryRaw<TagRow>("select word,status,`desc` from keyword where type='tag'").ToList()
                .ToDictionary(x => x.word, x => x.desc);
        }
        private class TagRow
        {
            public string word { get; set; }
            public string status { get; set; }
            public string desc { get; set; }
        }
        private List<User> InitUsers(List<User> users)
        {
            foreach (var user in users)
            {
                user.displayId = user.userId.ToString();
                user.displayText = user.userName;
            }
            return users;
        }
        public async Task<int> GetQueueUpdateInterval()
        {
            return (await base.Database.SqlQueryRaw<int>("SELECT datediff(NOW(),QueueUpdateTime) FROM status WHERE id='Current'").ToListAsync())[0];
        }
        public async Task<string> GetQueue()
        {
            return (await base.Database.SqlQueryRaw<string>("SELECT Queue FROM status WHERE id='Current'").ToListAsync())[0];
        }
        public User GetUserByIdSync(int userId)
        {
            return InitUsers(Users.Where(x => x.userId == userId).ToList()).FirstOrDefault();
        }
        public async Task<List<User>> GetFollowedUser(bool followed = true, bool validOnly = false)
        {
            return InitUsers(await Users.Where(x => x.followed == followed && (!validOnly || !x.invalid)).ToListAsync());
        }
        public async Task<List<User>> GetQueuedUser(bool validOnly = false)
        {
            return InitUsers(await Users.Where(x => x.queued && (!validOnly || !x.invalid)).ToListAsync());
        }
        public async Task<List<User>> GetUnFollowedUserNeedUpdate(DateTime time)
        {
            return InitUsers(await Users.FromSqlRaw("select * from user where followed=0 and queued=0 and `invalid`=false and (userName=\"\" or updateTime<{0})", time.ToString("yyyy-MM-dd HH:mm:ss")).ToListAsync());
        }
        public async Task<List<User>> GetQueuedOrFollowedUserStatusUpdateBatch(int count)
        {
            return InitUsers(await Users.FromSqlRaw("select * from user where (followed=true or queued=true) and `invalid`=false order by updateTime limit {0}", count).ToListAsync());
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
            using (var ts = base.Database.BeginTransaction())
            {
                try
                {
                    int affected = 0;
                    using(var cmd = new MySqlCommand("update user set followed=false"))
                        base.Database.ExecuteSqlRaw(cmd.CommandText, cmd.Parameters.Cast<object>());
                    foreach (var user in data)
                    {
                        string cmdText = "insert into user(userId,userName,followed,queued,updateTime) values(@0,@1,@2,@3,NOW()) on duplicate key update userName=@1,followed=@2,queued=@3,updateTime=NOW(),`invalid`=false;\n";
                        var cmd = new MySqlCommand(cmdText);
                        cmd.Parameters.AddWithValue("@0", user.userId);
                        cmd.Parameters.AddWithValue("@1", user.userName);
                        cmd.Parameters.AddWithValue("@2", user.followed);
                        cmd.Parameters.AddWithValue("@3", user.queued);
                        affected += base.Database.ExecuteSqlRaw(cmd.CommandText, cmd.Parameters.Cast<object>());
                    }
                    ts.Commit();
                    Console.WriteLine("Affected:" + affected);
                }
                catch (MySqlException e)
                {
                    Console.Error.WriteLine(e.Message);
                    ts.Rollback();
                    throw;
                }
            }
        }
        public void UpdateUserNameAndValid(List<User> data)
        {
            using (var ts = base.Database.BeginTransaction())
            {
                try
                {
                    int affected = 0;
                    foreach (var user in data)
                    {
                        string cmdText = "insert into user(userId,userName,followed,queued,updateTime,`invalid`) values(@0,@1,false,false,NOW(),@2) on duplicate key update userId=@0,userName=@1,updateTime=NOW(),`invalid`=@2;\n";
                        var cmd = new MySqlCommand(cmdText);
                        cmd.Parameters.AddWithValue("@0", user.userId);
                        cmd.Parameters.AddWithValue("@1", user.userName);
                        cmd.Parameters.AddWithValue("@2", user.invalid);
                        affected += base.Database.ExecuteSqlRaw(cmd.CommandText, cmd.Parameters.Cast<object>());
                    }
                    ts.Commit();
                    Console.WriteLine("Affected:" + affected);
                }
                catch (MySqlException e)
                {
                    Console.Error.WriteLine(e.Message);
                    ts.Rollback();
                    throw;
                }
            }
        }
        public async Task UpdateUser(User user)
        {
            await StandardNoneQuery("insert into user(userId,userName,followed,queued,updateTime) values(@0,@1,@2,@3,NOW()) on duplicate key update userName=@1,followed=@2,queued=@3,updateTime=NOW();\n"
                , (cmd) => {
                    cmd.Parameters.AddWithValue("@0", user.userId);
                    cmd.Parameters.AddWithValue("@1", user.userName);
                    cmd.Parameters.AddWithValue("@2", user.followed);
                    cmd.Parameters.AddWithValue("@3", user.queued);
                });
        }
        /*插入或更新illust
        注意：如果illust已经存在，readed/bookmarked/bookmarkPrivate/bookmarkEach的本地数据优先于远程数据，因此不更新
        */
        public void UpdateIllustOriginalData(List<Illust> data)
        {
            using (var ts = base.Database.BeginTransaction())
            {
                try
                {
                    int affected = 0;
                    foreach (var illust in data)
                        if(illust.valid)
                        {
                            string cmdText = "insert ignore user(userId) values(@userId);\n" +
                                             "insert into illust values(@0,@1,@2,@3,@4,@5,@6,@7,@8,@9,@10,@11,@12,@13,@14,@15,@16,@17,NOW(),@18,@19,@20,@21)" +
                                             "on duplicate key update id=@0,title=@1,description=@2,xRestrict=@3,tags=@4," +
                                             "userId=@5,width=@6,height=@7,pageCount=@8," +
                                             "urlFormat=@11,urlThumbFormat=@12,valid=@15,likeCount=@16,bookmarkCount=@17,updateTime=NOW(),"+
                                             "ugoiraFrames=@18,ugoiraURL=@19,viewCount=@20,uploadDate=@21;\n";
                            var cmd = new MySqlCommand(cmdText);
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
                            cmd.Parameters.AddWithValue("@20", illust.viewCount);
                            cmd.Parameters.AddWithValue("@21", illust.uploadDate);
                            affected += base.Database.ExecuteSqlRaw(cmd.CommandText, cmd.Parameters.Cast<object>());
                        }
                        else
                        {
                            string cmdText = "insert ignore user(userId) values(@userId);\n" +
                                             "insert into illust(id,updateTime,valid) values(@0,NOW(),@1)" +
                                             "on duplicate key update updateTime=NOW(),valid=@1;\n";
                            var cmd = new MySqlCommand(cmdText);
                            cmd.Parameters.AddWithValue("@userId", illust.userId);
                            cmd.Parameters.AddWithValue("@0", illust.id);
                            cmd.Parameters.AddWithValue("@1", illust.valid);
                            affected += base.Database.ExecuteSqlRaw(cmd.CommandText, cmd.Parameters.Cast<object>());
                        }
                    ts.Commit();
                    Console.WriteLine("Affected:"+affected);
                }
                catch (MySqlException e)
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
        public async Task UpdateUserAgent(string userAgent)
        {
            await StandardNoneQuery("update `status` set UserAgentCache=@0 where id=\"Current\";", (cmd) => { cmd.Parameters.AddWithValue("@0", userAgent); });
        }


        public async Task<int> StandardNoneQuery(string cmd_text, Action<MySqlCommand> add_para)
        {
            using var cmd = new MySqlCommand(cmd_text);
            add_para(cmd);
            int ret = await base.Database.ExecuteSqlRawAsync(cmd.CommandText, cmd.Parameters.Cast<object>());
            Console.WriteLine("Update {0} Rows", ret);
            return ret;
        }
    }
}
