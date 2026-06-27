using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PictureSpider.Migrations.Manhuagui
{
    [DbContext(typeof(PictureSpider.Manhuagui.Database))]
    [Migration("20260627010000_RenameManhuaguiEnabledToFav")]
    public partial class RenameManhuaguiEnabledToFav : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            RenameColumnIfNeeded(migrationBuilder, "comic", "Enabled", "Fav");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            RenameColumnIfNeeded(migrationBuilder, "comic", "Fav", "Enabled");
        }

        private static void RenameColumnIfNeeded(MigrationBuilder migrationBuilder, string table, string oldColumn, string newColumn)
        {
            var procedure = $"RenameManhuagui{table}{oldColumn}To{newColumn}";
            migrationBuilder.Sql($@"DROP PROCEDURE IF EXISTS `{procedure}`;");
            migrationBuilder.Sql($@"CREATE PROCEDURE `{procedure}`()
BEGIN
    IF EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = '{table}'
          AND COLUMN_NAME = '{oldColumn}'
    ) AND NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = '{table}'
          AND COLUMN_NAME = '{newColumn}'
    ) THEN
        ALTER TABLE `{table}` CHANGE COLUMN `{oldColumn}` `{newColumn}` tinyint(1) NOT NULL DEFAULT 1;
    END IF;
END;");
            migrationBuilder.Sql($@"CALL `{procedure}`();");
            migrationBuilder.Sql($@"DROP PROCEDURE `{procedure}`;");
        }
    }
}
