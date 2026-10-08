using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PictureSpider.Migrations.Pixiv
{
    [DbContext(typeof(PictureSpider.Pixiv.Database))]
    [Migration("20261008090000_AddPixivPendingUiOperations")]
    public partial class AddPixivPendingUiOperations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS `PendingUiOperations` (
                `Id` bigint NOT NULL AUTO_INCREMENT,
                `CreatedAt` datetime(6) NOT NULL,
                `Kind` enum('SetReaded','SetBookmarked','SetPageExcluded','SetUserFollowOrQueue','AddQueuedUser','SetTagStatus','SetLoginInfo') NOT NULL,
                `TargetKey` varchar(128) NOT NULL,
                `Value` int NOT NULL,
                `Cookie` text NULL,
                `UserAgent` text NULL,
                PRIMARY KEY (`Id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PendingUiOperations");
        }
    }
}
