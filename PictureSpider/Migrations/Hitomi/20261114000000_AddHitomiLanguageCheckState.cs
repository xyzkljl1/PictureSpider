using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PictureSpider.Hitomi;

#nullable disable

namespace PictureSpider.Migrations.DatabaseMigrations
{
    [DbContext(typeof(Database))]
    [Migration("20261114000000_AddHitomiLanguageCheckState")]
    public partial class AddHitomiLanguageCheckState : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "languageCheckState",
                table: "IllustGroups",
                type: "enum('NotRequired','Pending','Excluded')",
                nullable: false,
                defaultValue: "NotRequired");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "languageCheckState",
                table: "IllustGroups");
        }
    }
}
