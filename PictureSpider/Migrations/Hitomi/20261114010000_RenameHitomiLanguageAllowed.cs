using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PictureSpider.Hitomi;

#nullable disable

namespace PictureSpider.Migrations.DatabaseMigrations
{
    [DbContext(typeof(Database))]
    [Migration("20261114010000_RenameHitomiLanguageAllowed")]
    public partial class RenameHitomiLanguageAllowed : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE `IllustGroups` MODIFY COLUMN `languageCheckState` enum('NotRequired','Pending','Excluded','Allowed') NOT NULL DEFAULT 'NotRequired'");
            migrationBuilder.Sql("UPDATE `IllustGroups` SET `languageCheckState` = 'Allowed' WHERE `languageCheckState` = 'NotRequired'");
            migrationBuilder.Sql("ALTER TABLE `IllustGroups` MODIFY COLUMN `languageCheckState` enum('Allowed','Pending','Excluded') NOT NULL DEFAULT 'Allowed'");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE `IllustGroups` MODIFY COLUMN `languageCheckState` enum('Allowed','Pending','Excluded','NotRequired') NOT NULL DEFAULT 'Allowed'");
            migrationBuilder.Sql("UPDATE `IllustGroups` SET `languageCheckState` = 'NotRequired' WHERE `languageCheckState` = 'Allowed'");
            migrationBuilder.Sql("ALTER TABLE `IllustGroups` MODIFY COLUMN `languageCheckState` enum('NotRequired','Pending','Excluded') NOT NULL DEFAULT 'NotRequired'");
        }
    }
}
