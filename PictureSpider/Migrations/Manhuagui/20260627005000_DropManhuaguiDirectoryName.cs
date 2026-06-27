using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PictureSpider.Migrations.Manhuagui
{
    [DbContext(typeof(PictureSpider.Manhuagui.Database))]
    [Migration("20260627005000_DropManhuaguiDirectoryName")]
    public partial class DropManhuaguiDirectoryName : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            DropColumnIfExists(migrationBuilder, "comic", "DirectoryName");
            DropColumnIfExists(migrationBuilder, "chapter", "DirectoryName");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            AddColumnIfMissing(migrationBuilder, "comic", "DirectoryName");
            AddColumnIfMissing(migrationBuilder, "chapter", "DirectoryName");
        }

        private static void DropColumnIfExists(MigrationBuilder migrationBuilder, string table, string column)
        {
            var procedure = $"DropManhuagui{table}{column}";
            migrationBuilder.Sql($@"DROP PROCEDURE IF EXISTS `{procedure}`;");
            migrationBuilder.Sql($@"CREATE PROCEDURE `{procedure}`()
BEGIN
    IF EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = '{table}'
          AND COLUMN_NAME = '{column}'
    ) THEN
        ALTER TABLE `{table}` DROP COLUMN `{column}`;
    END IF;
END;");
            migrationBuilder.Sql($@"CALL `{procedure}`();");
            migrationBuilder.Sql($@"DROP PROCEDURE `{procedure}`;");
        }

        private static void AddColumnIfMissing(MigrationBuilder migrationBuilder, string table, string column)
        {
            var procedure = $"AddManhuagui{table}{column}";
            migrationBuilder.Sql($@"DROP PROCEDURE IF EXISTS `{procedure}`;");
            migrationBuilder.Sql($@"CREATE PROCEDURE `{procedure}`()
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = '{table}'
          AND COLUMN_NAME = '{column}'
    ) THEN
        ALTER TABLE `{table}` ADD COLUMN `{column}` varchar(260) NOT NULL DEFAULT '';
    END IF;
END;");
            migrationBuilder.Sql($@"CALL `{procedure}`();");
            migrationBuilder.Sql($@"DROP PROCEDURE `{procedure}`;");
        }
    }
}
