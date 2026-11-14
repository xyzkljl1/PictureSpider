using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PictureSpider.Pawchive;

#nullable disable

namespace PictureSpider.Migrations.Pawchive
{
    [DbContext(typeof(Database))]
    [Migration("20261115000100_AddPawchiveGoogleDriveImageZip")]
    public class AddPawchiveGoogleDriveImageZip : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE `Users` MODIFY COLUMN `dowloadExternalWorks` enum('None','DirectExternal','KeyZipMega','GoogleDriveImageZip') NOT NULL DEFAULT 'None';");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE `Users` SET `dowloadExternalWorks` = 'None' WHERE `dowloadExternalWorks` = 'GoogleDriveImageZip';");
            migrationBuilder.Sql("ALTER TABLE `Users` MODIFY COLUMN `dowloadExternalWorks` enum('None','DirectExternal','KeyZipMega') NOT NULL DEFAULT 'None';");
        }
    }
}
