using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PictureSpider.Hitomi;

#nullable disable

namespace PictureSpider.Migrations.DatabaseMigrations
{
    [DbContext(typeof(Database))]
    [Migration("20260913000000_AddHitomiIsCollection")]
    public partial class AddHitomiIsCollection : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "isCollection",
                table: "IllustGroups",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "isCollection",
                table: "IllustGroups");
        }
    }
}
