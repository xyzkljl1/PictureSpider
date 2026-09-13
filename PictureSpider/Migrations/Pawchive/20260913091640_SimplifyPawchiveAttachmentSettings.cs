using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PictureSpider.Migrations.Pawchive
{
    /// <inheritdoc />
    public partial class SimplifyPawchiveAttachmentSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fold the master switch into each attachment setting before removing it.
            migrationBuilder.Sql("UPDATE `Users` SET `dowloadImageWorks` = `dowloadWorks` AND `dowloadImageWorks`, `dowloadVideoWorks` = `dowloadWorks` AND `dowloadVideoWorks`;");

            migrationBuilder.RenameColumn(
                name: "dowloadImageWorks",
                table: "Users",
                newName: "downloadAttachmentImages");

            migrationBuilder.RenameColumn(
                name: "dowloadVideoWorks",
                table: "Users",
                newName: "downloadAttachmentVideos");

            migrationBuilder.DropColumn(
                name: "dowloadWorks",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "downloadAttachmentImages",
                table: "Users",
                newName: "dowloadImageWorks");

            migrationBuilder.RenameColumn(
                name: "downloadAttachmentVideos",
                table: "Users",
                newName: "dowloadVideoWorks");

            // Restore the effective settings; the original master switch is no longer recoverable.
            migrationBuilder.AddColumn<bool>(
                name: "dowloadWorks",
                table: "Users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);
        }
    }
}
