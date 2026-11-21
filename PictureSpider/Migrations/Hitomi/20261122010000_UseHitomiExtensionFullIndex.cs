using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PictureSpider.Hitomi;

#nullable disable

namespace PictureSpider.Migrations.DatabaseMigrations
{
    [DbContext(typeof(Database))]
    [Migration("20261122010000_UseHitomiExtensionFullIndex")]
    public partial class UseHitomiExtensionFullIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE Illusts DROP INDEX IX_Illusts_ext, MODIFY COLUMN ext VARCHAR(16) NULL, ADD INDEX IX_Illusts_ext (ext)");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE Illusts DROP INDEX IX_Illusts_ext, MODIFY COLUMN ext LONGTEXT NULL, ADD INDEX IX_Illusts_ext (ext(5))");
        }
    }
}
