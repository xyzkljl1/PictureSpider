using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PictureSpider.Migrations.Twitter
{
    [DbContext(typeof(PictureSpider.Twitter.Database))]
    [Migration("20260528034500_AddTwitterMediaDownloadUnavailable")]
    public partial class AddTwitterMediaDownloadUnavailable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "download_unavailable",
                table: "media",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "download_unavailable",
                table: "media");
        }
    }
}
