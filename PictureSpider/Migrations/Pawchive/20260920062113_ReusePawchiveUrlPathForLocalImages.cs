using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PictureSpider.Migrations.Pawchive
{
    /// <inheritdoc />
    public partial class ReusePawchiveUrlPathForLocalImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "urlPath",
                table: "Works",
                type: "varchar(512)",
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(95)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql("UPDATE `Works` SET `urlPath` = `localPath` WHERE `localPath` IS NOT NULL");

            migrationBuilder.DropColumn(
                name: "localPath",
                table: "Works");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 原哈希主键已替换为路径，回退需恢复备份，避免截断路径或更改实体标识。
            throw new System.NotSupportedException("Restore the database backup to downgrade local image paths.");
        }
    }
}
