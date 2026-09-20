using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PictureSpider.Migrations.Pawchive
{
    /// <inheritdoc />
    public partial class AddPawchiveArchiveImageGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkGroups_WorkGroups_parentId_userservice",
                table: "WorkGroups");
            migrationBuilder.DropIndex(
                name: "IX_WorkGroups_parentId_userservice",
                table: "WorkGroups");

            migrationBuilder.AddColumn<string>(
                name: "localPath",
                table: "Works",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "sourceArchiveId",
                table: "WorkGroups",
                type: "varchar(95)",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "sourceArchiveType",
                table: "WorkGroups",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "WorkGroups",
                type: "enum('Images','NonImages')",
                nullable: false,
                defaultValue: "Images")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_WorkGroups_parentId_userservice",
                table: "WorkGroups",
                columns: new[] { "parentId", "userservice" });

            migrationBuilder.AddForeignKey(
                name: "FK_WorkGroups_WorkGroups_parentId_userservice",
                table: "WorkGroups",
                columns: new[] { "parentId", "userservice" },
                principalTable: "WorkGroups",
                principalColumns: new[] { "id", "userservice" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("UPDATE WorkGroups SET type='NonImages' WHERE parentId IS NOT NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_WorkGroups_sourceArchiveId_sourceArchiveType",
                table: "WorkGroups",
                columns: new[] { "sourceArchiveId", "sourceArchiveType" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkGroups_ExternalWorks_sourceArchiveId_sourceArchiveType",
                table: "WorkGroups",
                columns: new[] { "sourceArchiveId", "sourceArchiveType" },
                principalTable: "ExternalWorks",
                principalColumns: new[] { "id", "type" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 旧结构无法保存多个图片子组及独立的阅读/收藏状态，禁止静默丢弃这些数据。
            throw new System.NotSupportedException("Restore the database backup to downgrade archive image groups.");
        }
    }
}
