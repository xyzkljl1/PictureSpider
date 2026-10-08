using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PictureSpider.Migrations.Pixiv
{
    [DbContext(typeof(PictureSpider.Pixiv.Database))]
    [Migration("20261009090000_UsePixivPrimitiveTypes")]
    public partial class UsePixivPrimitiveTypes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 一次修改，避免逐列重建 illust 表。
            migrationBuilder.Sql("""
                ALTER TABLE illust
                    MODIFY COLUMN valid tinyint(1) NOT NULL DEFAULT 1,
                    MODIFY COLUMN width int NOT NULL DEFAULT 0,
                    MODIFY COLUMN height int NOT NULL DEFAULT 0,
                    MODIFY COLUMN pageCount int NOT NULL DEFAULT 1;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE illust
                    MODIFY COLUMN valid int NOT NULL DEFAULT 1,
                    MODIFY COLUMN width int unsigned NOT NULL DEFAULT 0,
                    MODIFY COLUMN height int unsigned NOT NULL DEFAULT 0,
                    MODIFY COLUMN pageCount int unsigned NOT NULL DEFAULT 1;
                """);
        }
    }
}
