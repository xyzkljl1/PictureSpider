using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace PictureSpider.AuthorHub
{
    public class Server : BaseServer
    {
        private readonly string connectStr;
        private readonly Dictionary<SourceModule, BaseServer> sourceServers;

        public Server(string connectionString, Dictionary<SourceModule, BaseServer> servers)
        {
            logPrefix = "AuthorHub";
            sourceServers = new Dictionary<SourceModule, BaseServer>(servers);
            try
            {
                var settings = new MySqlConnectionStringBuilder(connectionString);
                if (string.IsNullOrWhiteSpace(settings.Database))
                {
                    LogError("AuthorHubConnectStr must specify the AuthorHub database.");
                    return;
                }
                connectStr = settings.ConnectionString;
            }
            catch (ArgumentException)
            {
                LogError("Invalid AuthorHubConnectStr.");
            }
        }

        public override async Task<List<ExplorerQueue>> GetExplorerQueues()
        {
            var result = new List<ExplorerQueue>();
            if (string.IsNullOrWhiteSpace(connectStr))
                return result;

            List<Author> authors;
            List<AuthorSource> sources;
            try
            {
                using var db = new Database { ConnStr = connectStr, ReadOnly = true };
                authors = await db.Authors.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Id).ToListAsync();
                sources = await db.AuthorSources.AsNoTracking().ToListAsync();
            }
            catch (Exception ex) when (ex is MySqlException
                or InvalidOperationException { InnerException: MySqlException })
            {
                // Pomelo 会包装连接失败；两种形式统一处理，其它异常不在此捕获。
                var error = ex as MySqlException ?? (MySqlException)ex.InnerException;
                LogError($"Failed to read author mappings: MySQL {error.Number}.");
                return result;
            }

            return BuildExplorerQueues(authors, sources);
        }

        internal List<ExplorerQueue> BuildExplorerQueues(List<Author> authors, List<AuthorSource> sources)
        {
            var result = new List<ExplorerQueue>();
            var sourcesByAuthor = sources.ToLookup(x => x.AuthorId);
            foreach (var author in authors)
            {
                var authorSources = sourcesByAuthor[author.Id].ToList();
                if (authorSources.Select(x => x.Module).Distinct().Count() < 2)
                    continue;

                var modules = new HashSet<SourceModule>();
                bool followed = false;
                bool failed = false;
                foreach (var source in authorSources)
                {
                    if (string.IsNullOrWhiteSpace(source.SourceKey)
                        || !sourceServers.TryGetValue(source.Module, out var server))
                        continue;

                    BaseUser user;
                    try
                    {
                        user = server.GetUserById(source.SourceKey);
                    }
                    catch (Exception ex) when (ex is MySqlException
                        or MySql.Data.MySqlClient.MySqlException
                        or InvalidOperationException { InnerException: MySqlException })
                    {
                        var number = ex switch
                        {
                            MySqlException error => error.Number,
                            MySql.Data.MySqlClient.MySqlException error => error.Number,
                            _ => ((MySqlException)ex.InnerException).Number
                        };
                        LogError($"Failed to read {source.Module} author {author.Id}: MySQL {number}.");
                        failed = true;
                        break;
                    }
                    if (user is null)
                        continue;

                    var key = user is BaseUserEx userEx ? userEx.DbKey : user.displayId;
                    // 来源库可能忽略大小写或接受其它 ID 写法，只接受其返回的规范键。
                    if (!string.Equals(key, source.SourceKey, StringComparison.Ordinal))
                        continue;
                    modules.Add(source.Module);
                    followed |= user.followed;
                }

                if (!failed && followed && modules.Count >= 2)
                {
                    var names = modules.OrderBy(x => x).Select(x => x == SourceModule.Twitter ? "X" : x.ToString());
                    result.Add(new ExplorerQueue(ExplorerQueue.QueueType.User,
                        author.Id.ToString(CultureInfo.InvariantCulture),
                        $"A · {author.Name} [{string.Join(" / ", names)}]"));
                }
            }
            return result;
        }

        // 首版仅列出作者队列；图片列表及作者栏沿用 BaseServer 的空实现。
    }
}
