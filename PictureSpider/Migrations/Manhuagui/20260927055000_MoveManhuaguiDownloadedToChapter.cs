using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace PictureSpider.Migrations.Manhuagui
{
    [DbContext(typeof(PictureSpider.Manhuagui.Database))]
    [Migration("20260927055000_MoveManhuaguiDownloadedToChapter")]
    public partial class MoveManhuaguiDownloadedToChapter : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Downloaded",
                table: "chapter",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(@"UPDATE `chapter` AS `c`
LEFT JOIN (
    SELECT `ChapterId`, COUNT(*) AS `PageRows`, SUM(CASE WHEN `Downloaded` = 1 THEN 1 ELSE 0 END) AS `DownloadedPages`
    FROM `page`
    GROUP BY `ChapterId`
) AS `p` ON `p`.`ChapterId` = `c`.`Id`
SET `c`.`Downloaded` = CASE
    WHEN `c`.`PageCount` > 0
      AND `p`.`PageRows` = `c`.`PageCount`
      AND `p`.`DownloadedPages` = `c`.`PageCount` THEN 1
    ELSE 0
END;");

            migrationBuilder.DropColumn(
                name: "Downloaded",
                table: "page");

            migrationBuilder.DropColumn(
                name: "DownloadedAt",
                table: "page");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Downloaded",
                table: "page",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DownloadedAt",
                table: "page",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.Sql(@"UPDATE `page` AS `p`
INNER JOIN `chapter` AS `c` ON `c`.`Id` = `p`.`ChapterId`
SET `p`.`Downloaded` = `c`.`Downloaded`;");

            migrationBuilder.DropColumn(
                name: "Downloaded",
                table: "chapter");
        }
    }
}
