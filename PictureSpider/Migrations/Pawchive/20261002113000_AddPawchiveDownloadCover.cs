using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PictureSpider.Pawchive;

#nullable disable

namespace PictureSpider.Migrations.Pawchive
{
    [DbContext(typeof(Database))]
    [Migration("20261002113000_AddPawchiveDownloadCover")]
    public partial class AddPawchiveDownloadCover : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "downloadCover",
                table: "Users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);
            migrationBuilder.Sql("UPDATE `Users` SET `downloadCover` = `downloadAttachmentImages`;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "downloadCover",
                table: "Users");
        }
    }
}
