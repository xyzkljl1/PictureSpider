using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PictureSpider.Migrations.Manhuagui
{
    [DbContext(typeof(PictureSpider.Manhuagui.Database))]
    [Migration("20260626193000_ManhuaguiInitialSchema")]
    public partial class ManhuaguiInitialSchema : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS `comic` (
                `Id` int NOT NULL,
                `Title` varchar(500) NOT NULL DEFAULT '',
                `Enabled` tinyint(1) NOT NULL DEFAULT 1,
                `UpdatedAt` datetime(6) NOT NULL,
                `LastCheckedAt` datetime(6) NULL,
                PRIMARY KEY (`Id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS `chapter` (
                `Id` int NOT NULL,
                `ComicId` int NOT NULL,
                `Title` varchar(500) NOT NULL DEFAULT '',
                `Index` int NOT NULL,
                `PageCount` int NOT NULL,
                `UpdatedAt` datetime(6) NOT NULL,
                `LastFetchedAt` datetime(6) NULL,
                PRIMARY KEY (`Id`),
                KEY `IX_chapter_ComicId` (`ComicId`),
                CONSTRAINT `FK_chapter_comic_ComicId` FOREIGN KEY (`ComicId`) REFERENCES `comic` (`Id`) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS `page` (
                `Id` bigint NOT NULL AUTO_INCREMENT,
                `ChapterId` int NOT NULL,
                `Index` int NOT NULL,
                `ImagePath` varchar(1000) NOT NULL DEFAULT '',
                `FileName` varchar(260) NOT NULL DEFAULT '',
                `Downloaded` tinyint(1) NOT NULL DEFAULT 0,
                `DownloadedAt` datetime(6) NULL,
                `LastError` text NULL,
                PRIMARY KEY (`Id`),
                UNIQUE KEY `IX_page_ChapterId_Index` (`ChapterId`, `Index`),
                KEY `IX_page_ChapterId` (`ChapterId`),
                CONSTRAINT `FK_page_chapter_ChapterId` FOREIGN KEY (`ChapterId`) REFERENCES `chapter` (`Id`) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "page");
            migrationBuilder.DropTable(name: "chapter");
            migrationBuilder.DropTable(name: "comic");
        }
    }
}
