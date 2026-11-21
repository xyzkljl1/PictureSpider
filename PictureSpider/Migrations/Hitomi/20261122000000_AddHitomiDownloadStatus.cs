using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PictureSpider.Hitomi;

#nullable disable

namespace PictureSpider.Migrations.DatabaseMigrations
{
    [DbContext(typeof(Database))]
    [Migration("20261122000000_AddHitomiDownloadStatus")]
    public partial class AddHitomiDownloadStatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "downloadStatus", table: "IllustGroups",
                type: "enum('Downloading','Downloaded','Deleted')", nullable: false, defaultValue: "Downloading");
            migrationBuilder.CreateIndex(
                name: "IX_IllustGroups_fav_downloadStatus_readed", table: "IllustGroups",
                columns: new[] { "fav", "downloadStatus", "readed" });
            migrationBuilder.CreateIndex(name: "IX_Illusts_ext", table: "Illusts", column: "ext")
                .Annotation("MySql:IndexPrefixLength", new[] { 5 });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Illusts_ext", table: "Illusts");
            migrationBuilder.DropIndex(name: "IX_IllustGroups_fav_downloadStatus_readed", table: "IllustGroups");
            migrationBuilder.DropColumn(name: "downloadStatus", table: "IllustGroups");
        }
    }
}
