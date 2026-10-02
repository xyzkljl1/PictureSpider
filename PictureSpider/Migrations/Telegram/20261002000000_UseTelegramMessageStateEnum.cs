using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PictureSpider.Migrations.Telegram
{
    [DbContext(typeof(PictureSpider.Telegram.Database))]
    [Migration("20261002000000_UseTelegramMessageStateEnum")]
    public partial class UseTelegramMessageStateEnum : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Map names explicitly: MySQL ENUM ordinals start at 1, but MessageState starts at 0.
            // A stored generated column avoids a full-table UPDATE holding locks on every row.
            migrationBuilder.Sql("ALTER TABLE `Messages` ADD COLUMN `stateEnum` enum('Ignore','Wait','Done','Dup','NotFound') GENERATED ALWAYS AS (CASE `state` WHEN 0 THEN 'Ignore' WHEN 1 THEN 'Wait' WHEN 2 THEN 'Done' WHEN 3 THEN 'Dup' WHEN 4 THEN 'NotFound' END) STORED NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE `Messages` MODIFY COLUMN `stateEnum` enum('Ignore','Wait','Done','Dup','NotFound') NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE `Messages` DROP COLUMN `state`, CHANGE COLUMN `stateEnum` `state` enum('Ignore','Wait','Done','Dup','NotFound') NOT NULL AFTER `albumid`;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE `Messages` ADD COLUMN `stateNumber` int GENERATED ALWAYS AS (CASE `state` WHEN 'Ignore' THEN 0 WHEN 'Wait' THEN 1 WHEN 'Done' THEN 2 WHEN 'Dup' THEN 3 WHEN 'NotFound' THEN 4 END) STORED NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE `Messages` MODIFY COLUMN `stateNumber` int NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE `Messages` DROP COLUMN `state`, CHANGE COLUMN `stateNumber` `state` int NOT NULL AFTER `albumid`;");
        }
    }
}
