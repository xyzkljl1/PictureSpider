using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PictureSpider.Migrations.Pawchive
{
    /// <inheritdoc />
    public partial class SplitPawchiveNonImageGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "parentId",
                table: "WorkGroups",
                type: "varchar(95)",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_WorkGroups_parentId_userservice",
                table: "WorkGroups",
                columns: new[] { "parentId", "userservice" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkGroups_WorkGroups_parentId_userservice",
                table: "WorkGroups",
                columns: new[] { "parentId", "userservice" },
                principalTable: "WorkGroups",
                principalColumns: new[] { "id", "userservice" },
                onDelete: ReferentialAction.Restrict);

            // 保留原帖和文件状态，只将非图片文件改挂到独立的负数ID子组。
            migrationBuilder.Sql("""
                CREATE TEMPORARY TABLE PawNonImageGroups AS
                SELECT g.id AS parentId, g.userservice,
                    CAST((SELECT COALESCE(MIN(CAST(id AS SIGNED)), 0) FROM WorkGroups WHERE id LIKE '-%')
                        - CAST(ROW_NUMBER() OVER (ORDER BY g.userservice, g.id) AS SIGNED) AS CHAR(95)) AS childId
                FROM WorkGroups g JOIN (
                    SELECT workGroupid AS parentId, workGroupuserservice AS userservice FROM Works
                    WHERE workGroupid IS NOT NULL AND LOWER(COALESCE(name, '')) NOT REGEXP '[.](jpeg|jpg|png|gif|webp|bmp|jfif|jpe|avif)$'
                    UNION
                    SELECT coverGroupid, coverGroupuserservice FROM Works
                    WHERE coverGroupid IS NOT NULL AND LOWER(COALESCE(name, '')) NOT REGEXP '[.](jpeg|jpg|png|gif|webp|bmp|jfif|jpe|avif)$'
                    UNION
                    SELECT workGroupid, workGroupuserservice FROM ExternalWorks
                    WHERE workGroupid IS NOT NULL AND LOWER(COALESCE(name, '')) NOT REGEXP '[.](jpeg|jpg|png|gif|webp|bmp|jfif|jpe|avif)$'
                ) files ON files.parentId=g.id AND files.userservice=g.userservice
                WHERE g.parentId IS NULL;

                INSERT INTO WorkGroups (id, userservice, parentId, title, `desc`, embedUrl, readed, fav, fetched, userid)
                SELECT m.childId, g.userservice, g.id, g.title, g.`desc`, g.embedUrl, false, g.fav, g.fetched, g.userid
                FROM WorkGroups g JOIN PawNonImageGroups m ON m.parentId=g.id AND m.userservice=g.userservice;

                UPDATE Works w JOIN PawNonImageGroups m ON m.parentId=w.workGroupid AND m.userservice=w.workGroupuserservice
                SET w.workGroupid=m.childId
                WHERE LOWER(COALESCE(w.name, '')) NOT REGEXP '[.](jpeg|jpg|png|gif|webp|bmp|jfif|jpe|avif)$';
                UPDATE Works w JOIN PawNonImageGroups m ON m.parentId=w.coverGroupid AND m.userservice=w.coverGroupuserservice
                SET w.coverGroupid=m.childId
                WHERE LOWER(COALESCE(w.name, '')) NOT REGEXP '[.](jpeg|jpg|png|gif|webp|bmp|jfif|jpe|avif)$';
                UPDATE ExternalWorks w JOIN PawNonImageGroups m ON m.parentId=w.workGroupid AND m.userservice=w.workGroupuserservice
                SET w.workGroupid=m.childId
                WHERE LOWER(COALESCE(w.name, '')) NOT REGEXP '[.](jpeg|jpg|png|gif|webp|bmp|jfif|jpe|avif)$';

                UPDATE WorkGroups g JOIN Users u ON u.id=g.userid AND u.service=g.userservice
                SET g.readed = (
                    EXISTS (SELECT 1 FROM Works w WHERE w.workGroupid=g.id AND w.workGroupuserservice=g.userservice
                        AND u.downloadAttachmentVideos AND LOWER(COALESCE(w.name, '')) REGEXP '[.](avi|mp4|divx|wmv|rmvb|mkv)$'
                        AND (NOT g.fav OR NOT w.excluded))
                    OR EXISTS (SELECT 1 FROM ExternalWorks w WHERE w.workGroupid=g.id AND w.workGroupuserservice=g.userservice
                        AND u.dowloadExternalWorks<>'None' AND (NOT g.fav OR NOT w.excluded)))
                    AND NOT EXISTS (SELECT 1 FROM Works w WHERE w.workGroupid=g.id AND w.workGroupuserservice=g.userservice
                        AND u.downloadAttachmentVideos AND LOWER(COALESCE(w.name, '')) REGEXP '[.](avi|mp4|divx|wmv|rmvb|mkv)$'
                        AND (NOT g.fav OR NOT w.excluded) AND NOT w.readed)
                    AND NOT EXISTS (SELECT 1 FROM ExternalWorks w WHERE w.workGroupid=g.id AND w.workGroupuserservice=g.userservice
                        AND u.dowloadExternalWorks<>'None' AND (NOT g.fav OR NOT w.excluded) AND NOT w.readed)
                WHERE g.parentId IS NOT NULL;
                DROP TEMPORARY TABLE PawNonImageGroups;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 撤销拆组时还原文件归属，文件自身的已下载标记不变。
            migrationBuilder.Sql("""
                UPDATE Works w JOIN WorkGroups g ON g.id=w.workGroupid AND g.userservice=w.workGroupuserservice
                SET w.workGroupid=g.parentId WHERE g.parentId IS NOT NULL;
                UPDATE Works w JOIN WorkGroups g ON g.id=w.coverGroupid AND g.userservice=w.coverGroupuserservice
                SET w.coverGroupid=g.parentId WHERE g.parentId IS NOT NULL;
                UPDATE ExternalWorks w JOIN WorkGroups g ON g.id=w.workGroupid AND g.userservice=w.workGroupuserservice
                SET w.workGroupid=g.parentId WHERE g.parentId IS NOT NULL;
                DELETE FROM WorkGroups WHERE parentId IS NOT NULL;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_WorkGroups_WorkGroups_parentId_userservice",
                table: "WorkGroups");

            migrationBuilder.DropIndex(
                name: "IX_WorkGroups_parentId_userservice",
                table: "WorkGroups");

            migrationBuilder.DropColumn(
                name: "parentId",
                table: "WorkGroups");
        }
    }
}
