using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PictureSpider.Migrations.DatabaseMigrations
{
    /// <inheritdoc />
    public partial class lsf_dev3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Readed",
                table: "Readed");

            migrationBuilder.RenameTable(
                name: "Readed",
                newName: "Waited");

            migrationBuilder.AddColumn<bool>(
                name: "fav",
                table: "Waited",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Waited",
                table: "Waited",
                column: "path");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Waited",
                table: "Waited");

            migrationBuilder.DropColumn(
                name: "fav",
                table: "Waited");

            migrationBuilder.RenameTable(
                name: "Waited",
                newName: "Readed");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Readed",
                table: "Readed",
                column: "path");
        }
    }
}
