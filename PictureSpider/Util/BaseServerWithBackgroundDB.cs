using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PictureSpider
{
    /*
     * 所有UI/Listener操作使用临时数据库，不立刻执行修改而是将指令存入数据库的PendingUiOperations表中，后台线程定期检查并执行这些指令
     * 只有后台线程会修改数据，保证串行，后台线程执行定期任务和UI发过来的指令
     * 通过保证不追踪PendingUiOperations实体并且后台执行ChangeTracker.Clear()，确保后台能获得最新的PendingUiOperations数据
     * 这是为了保证数据库操作串行，后台总能获得最新的数据，且不阻塞UI。
     */
    public abstract class BaseServerWithBackgroundDB<DatabaseType> : BaseServerWithDB<DatabaseType>
        where DatabaseType : BaseBackgroundEFDatabase, new()
    {
        protected override DatabaseType database => databaseSchedule;

        protected BaseServerWithBackgroundDB(string connStr = "") : base(connStr)
        {
        }

        protected async Task QueuePendingUiOperation(PendingUiOperation operation)
        {
            operation.CreatedAt = DateTime.UtcNow;
            using var db = NewDbContext();
            db.PendingUiOperations.Add(operation);
            await db.SaveChangesAsync();
        }

        public override Task SetUserFollowOrQueue(BaseUser user)
        {
            var userEx = (BaseUserEx)user;
            return QueuePendingUiOperation(new PendingUiOperation
            {
                Kind = PendingUiOperationKind.SetUserFollowOrQueue,
                TargetKey = userEx.DbKey,
                Value = (int)userEx.FollowQueueStatus
            });
        }
        public override Task SetReaded(ExplorerFileBase file)
        {
            var fileEx = (ExplorerFileBaseEx)file;
            return QueuePendingUiOperation(new PendingUiOperation
            {
                Kind = PendingUiOperationKind.SetReaded,
                TargetKey = fileEx.DbKey,
                Value = fileEx.readed ? 1 : 0
            });
        }

        public override Task SetBookmarked(ExplorerFileBase file)
        {
            var fileEx = (ExplorerFileBaseEx)file;
            return QueuePendingUiOperation(new PendingUiOperation
            {
                Kind = PendingUiOperationKind.SetBookmarked,
                TargetKey = fileEx.DbKey,
                Value = fileEx.bookmarked ? 1 : 0
            });
        }

        public override Task SetBookmarkEach(ExplorerFileBase file, int page)
        {
            var fileEx = (ExplorerFileBaseEx)file;
            if (page < 0 || page >= fileEx.pageCount())
                return Task.CompletedTask;
            var excluded = !fileEx.isPageValid(page);
            return QueuePendingUiOperation(new PendingUiOperation
            {
                Kind = PendingUiOperationKind.SetPageExcluded,
                TargetKey = fileEx.GetPageDbKey(page),
                Value = excluded ? 1 : 0
            });
        }
        protected async Task ApplyPendingUiOperations()
        {
            while (true)
            {
                var operations = await database.PendingUiOperations
                    .OrderBy(x => x.Id)
                    .Take(1000)
                    .ToListAsync();
                if (operations.Count == 0)
                    return;

                foreach (var operation in operations)
                    await ApplyPendingUiOperation(operation);
                database.PendingUiOperations.RemoveRange(operations);
                await database.SaveChangesAsync();
                database.ChangeTracker.Clear();
                if (operations.Count < 1000)
                    return;
            }
        }

        protected abstract Task ApplyPendingUiOperation(PendingUiOperation operation);
    }
}
