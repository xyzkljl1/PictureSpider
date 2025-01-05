using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PictureSpider.Migrations.DatabaseMigrations
{
    /// <inheritdoc />
    public partial class kemono5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ExternalWorks",
                table: "ExternalWorks");

            migrationBuilder.AlterColumn<string>(
                name: "url",
                table: "ExternalWorks",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(95)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "id",
                table: "ExternalWorks",
                type: "varchar(95)",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ExternalWorks",
                table: "ExternalWorks",
                columns: new[] { "id", "type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ExternalWorks",
                table: "ExternalWorks");

            migrationBuilder.DropColumn(
                name: "id",
                table: "ExternalWorks");

            migrationBuilder.UpdateData(
                table: "ExternalWorks",
                keyColumn: "url",
                keyValue: null,
                column: "url",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "url",
                table: "ExternalWorks",
                type: "varchar(95)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ExternalWorks",
                table: "ExternalWorks",
                columns: new[] { "url", "type" });
        }
    }
}
