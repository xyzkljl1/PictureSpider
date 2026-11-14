using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PictureSpider.Migrations.Telegram
{
    [DbContext(typeof(PictureSpider.Telegram.Database))]
    [Migration("20261114020000_AddTelegramIgnoredTitleTags")]
    public partial class AddTelegramIgnoredTitleTags : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IgnoredTitleTags",
                columns: table => new
                {
                    tag = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IgnoredTitleTags", x => x.tag);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.InsertData(
                table: "IgnoredTitleTags",
                columns: new[] { "tag" },
                columnTypes: new[] { "varchar(255)" },
                values: new object[,] { { "cos" }, { "写真" }, { "来自投稿" }, { "韩国专区" } });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "IgnoredTitleTags");
        }
    }
}
