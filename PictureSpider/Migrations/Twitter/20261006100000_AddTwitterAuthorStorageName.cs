using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PictureSpider.Migrations.Twitter
{
    [DbContext(typeof(PictureSpider.Twitter.Database))]
    [Migration("20261006100000_AddTwitterAuthorStorageName")]
    public partial class AddTwitterAuthorStorageName : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 外部作者表不加入本模块的 EF 模型，列、索引和跨库外键一起建立。
            migrationBuilder.Sql(@"
ALTER TABLE `user`
    ADD COLUMN `AuthorStorageName` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_ci NULL DEFAULT NULL,
    ADD INDEX `IX_user_AuthorStorageName` (`AuthorStorageName`),
    ADD CONSTRAINT `FK_user_AuthorHub_StorageName` FOREIGN KEY (`AuthorStorageName`)
        REFERENCES `authorhub`.`Authors` (`StorageName`) ON DELETE RESTRICT ON UPDATE RESTRICT;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE `user`
    DROP FOREIGN KEY `FK_user_AuthorHub_StorageName`,
    DROP INDEX `IX_user_AuthorStorageName`,
    DROP COLUMN `AuthorStorageName`;");
        }
    }
}
