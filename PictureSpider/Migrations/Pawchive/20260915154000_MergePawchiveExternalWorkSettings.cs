using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PictureSpider.Pawchive;

#nullable disable

namespace PictureSpider.Migrations.Pawchive
{
    [DbContext(typeof(Database))]
    [Migration("20260915154000_MergePawchiveExternalWorkSettings")]
    public partial class MergePawchiveExternalWorkSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE `Users` ADD COLUMN `downloadExternalWorkType` enum('None','DirectExternal','KeyZipMega') NOT NULL DEFAULT 'None';");
            migrationBuilder.Sql("UPDATE `Users` SET `downloadExternalWorkType` = CASE WHEN `downloadSpecial` = 'KeyZipMega' THEN 'KeyZipMega' WHEN `dowloadExternalWorks` = 1 THEN 'DirectExternal' ELSE 'None' END;");
            migrationBuilder.Sql("ALTER TABLE `Users` DROP COLUMN `downloadSpecial`, DROP COLUMN `dowloadExternalWorks`, CHANGE COLUMN `downloadExternalWorkType` `dowloadExternalWorks` enum('None','DirectExternal','KeyZipMega') NOT NULL DEFAULT 'None';");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE `Users` ADD COLUMN `dowloadExternalWorksLegacy` tinyint(1) NOT NULL DEFAULT 0, ADD COLUMN `downloadSpecial` enum('None','KeyZipMega') NOT NULL DEFAULT 'None';");
            migrationBuilder.Sql("UPDATE `Users` SET `dowloadExternalWorksLegacy` = (`dowloadExternalWorks` = 'DirectExternal'), `downloadSpecial` = CASE WHEN `dowloadExternalWorks` = 'KeyZipMega' THEN 'KeyZipMega' ELSE 'None' END;");
            migrationBuilder.Sql("ALTER TABLE `Users` DROP COLUMN `dowloadExternalWorks`, CHANGE COLUMN `dowloadExternalWorksLegacy` `dowloadExternalWorks` tinyint(1) NOT NULL DEFAULT 0;");
        }
    }
}
