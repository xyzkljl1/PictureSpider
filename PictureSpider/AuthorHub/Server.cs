using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
                authors = await db.Authors.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Id)
                    .Select(x => new Author { Id = x.Id, Name = x.Name }).ToListAsync();
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
                    result.Add(new ExplorerQueue(ExplorerQueue.QueueType.User,
                        author.Id.ToString(CultureInfo.InvariantCulture),
                        $"A · {author.Name}"));
                }
            }
            return result;
        }

        public override async Task<List<ExplorerFileBase>> GetExplorerQueueItems(ExplorerQueue queue)
        {
            var result = new List<ExplorerFileBase>();
            if (queue.type != ExplorerQueue.QueueType.User || string.IsNullOrWhiteSpace(connectStr)
                || !long.TryParse(queue.id, NumberStyles.None, CultureInfo.InvariantCulture, out var authorId))
                return result;

            List<AuthorSource> sources;
            try
            {
                using var db = new Database { ConnStr = connectStr, ReadOnly = true };
                sources = await db.AuthorSources.AsNoTracking()
                    .Where(x => x.AuthorId == authorId && db.Authors.Any(a => a.Id == x.AuthorId))
                    .OrderBy(x => x.Module).ThenBy(x => x.SourceKey).ToListAsync();
            }
            catch (Exception ex) when (ex is MySqlException
                or InvalidOperationException { InnerException: MySqlException })
            {
                var error = ex as MySqlException ?? (MySqlException)ex.InnerException;
                LogError($"Failed to read author {authorId} mappings: MySQL {error.Number}.");
                return result;
            }

            return await LoadExplorerFiles(sources);
        }

        internal async Task<List<ExplorerFileBase>> LoadExplorerFiles(List<AuthorSource> sources)
        {
            var result = new List<ExplorerFileBase>();
            foreach (var source in sources)
            {
                if (string.IsNullOrWhiteSpace(source.SourceKey)
                    || !sourceServers.TryGetValue(source.Module, out var server))
                    continue;
                try
                {
                    var user = server.GetUserById(source.SourceKey);
                    if (user is null)
                        continue;
                    var key = user is BaseUserEx userEx ? userEx.DbKey : user.displayId;
                    if (!string.Equals(key, source.SourceKey, StringComparison.Ordinal))
                        continue;

                    var files = await server.GetExplorerQueueItems(new ExplorerQueue(
                        ExplorerQueue.QueueType.User, source.SourceKey, user.displayText));
                    foreach (var file in files)
                    {
                        if (!file.bookmarked)
                            continue;
                        if (!Enumerable.Range(0, file.pageCount()).Any(page =>
                            file.isPageValid(page) && File.Exists(file.FilePath(page))))
                            continue;
                        result.Add(file);
                    }
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
                    LogError($"Failed to read {source.Module} works for author {source.AuthorId}: MySQL {number}.");
                }
            }
            return result;
        }

        private BaseServer GetSourceServer(object item)
        {
            SourceModule? module = item switch
            {
                Pixiv.ExplorerFile or Pixiv.User => SourceModule.Pixiv,
                Twitter.ExplorerFile or Twitter.User => SourceModule.Twitter,
                Hitomi.ExplorerFile or Hitomi.User => SourceModule.Hitomi,
                Kemono.ExplorerFile or Kemono.User => SourceModule.Kemono,
                Pawchive.ExplorerFile or Pawchive.User => SourceModule.Pawchive,
                _ => null
            };
            if (module.HasValue && sourceServers.TryGetValue(module.Value, out var server))
                return server;
            if (item != null)
                LogError($"No source server for type {item.GetType().FullName}.");
            return null;
        }

        public override bool UsesTripleBookmark(ExplorerFileBase file)
        {
            return GetSourceServer(file)?.UsesTripleBookmark(file) ?? false;
        }
        public override Task SetReaded(ExplorerFileBase file)
        {
            return GetSourceServer(file)?.SetReaded(file) ?? Task.CompletedTask;
        }
        public override Task SetBookmarked(ExplorerFileBase file)
        {
            return GetSourceServer(file)?.SetBookmarked(file) ?? Task.CompletedTask;
        }
        public override Task SetBookmarkEach(ExplorerFileBase file, int page)
        {
            return GetSourceServer(file)?.SetBookmarkEach(file, page) ?? Task.CompletedTask;
        }
        public override BaseUser GetUserById(string id, ExplorerFileBase file)
        {
            return GetSourceServer(file)?.GetUserById(id);
        }
        public override Task SetUserFollowOrQueue(BaseUser user)
        {
            return GetSourceServer(user)?.SetUserFollowOrQueue(user) ?? Task.CompletedTask;
        }
        public override Dictionary<string, TagStatus> GetAllTagsStatus(ExplorerFileBase file)
        {
            return GetSourceServer(file)?.GetAllTagsStatus() ?? new Dictionary<string, TagStatus>();
        }
        public override Dictionary<string, string> GetAllTagsDesc(ExplorerFileBase file)
        {
            return GetSourceServer(file)?.GetAllTagsDesc() ?? new Dictionary<string, string>();
        }
        public override Task UpdateTagStatus(string tag, TagStatus status, ExplorerFileBase file)
        {
            return GetSourceServer(file)?.UpdateTagStatus(tag, status) ?? Task.CompletedTask;
        }
    }
}
