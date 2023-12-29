using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PictureSpider.Migrations.DatabaseMigrations
{
    /// <inheritdoc />
    public partial class lsf_dev4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "path",
                table: "Waited",
                type: "varchar(600)",
                maxLength: 600,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(95)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "path",
                table: "Waited",
                type: "varchar(95)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(600)",
                oldMaxLength: 600)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
