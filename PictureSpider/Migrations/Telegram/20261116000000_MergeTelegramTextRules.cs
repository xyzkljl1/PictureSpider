using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PictureSpider.Migrations.Telegram
{
    [DbContext(typeof(PictureSpider.Telegram.Database))]
    [Migration("20261116000000_MergeTelegramTextRules")]
    public partial class MergeTelegramTextRules : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TextRules",
                columns: table => new
                {
                    type = table.Column<string>(type: "enum('BlockKeyword','IgnoreTag','IgnorePhrase')", nullable: false),
                    text = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TextRules", x => new { x.type, x.text });
                })
                .Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.Sql("INSERT INTO `TextRules` (`type`, `text`) "
                + "SELECT 'BlockKeyword', `keyword` FROM `BlockedKeywords` "
                + "UNION ALL SELECT 'IgnoreTag', `tag` FROM `IgnoredTitleTags`;");
            migrationBuilder.DropTable(name: "BlockedKeywords");
            migrationBuilder.DropTable(name: "IgnoredTitleTags");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("IgnorePhrase rules have no equivalent in the previous schema; restore rule data before reverting.");
        }
    }
}
