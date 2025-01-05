using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PictureSpider.Migrations.DatabaseMigrations
{
    /// <inheritdoc />
    public partial class kemono6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "fav",
                table: "Works",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "readed",
                table: "Works",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "fav",
                table: "ExternalWorks",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "readed",
                table: "ExternalWorks",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fav",
                table: "Works");

            migrationBuilder.DropColumn(
                name: "readed",
                table: "Works");

            migrationBuilder.DropColumn(
                name: "fav",
                table: "ExternalWorks");

            migrationBuilder.DropColumn(
                name: "readed",
                table: "ExternalWorks");
        }
    }
}
