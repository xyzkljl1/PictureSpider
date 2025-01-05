using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PictureSpider.Migrations.DatabaseMigrations
{
    /// <inheritdoc />
    public partial class kemono3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "urlHost",
                table: "ExternalWorks");

            migrationBuilder.RenameColumn(
                name: "urlPath",
                table: "ExternalWorks",
                newName: "url");

            migrationBuilder.AddColumn<int>(
                name: "type",
                table: "ExternalWorks",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "type",
                table: "ExternalWorks");

            migrationBuilder.RenameColumn(
                name: "url",
                table: "ExternalWorks",
                newName: "urlPath");

            migrationBuilder.AddColumn<string>(
                name: "urlHost",
                table: "ExternalWorks",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
