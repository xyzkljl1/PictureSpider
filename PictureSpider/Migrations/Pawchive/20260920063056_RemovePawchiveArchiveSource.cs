using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PictureSpider.Migrations.Pawchive
{
    /// <inheritdoc />
    public partial class RemovePawchiveArchiveSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkGroups_ExternalWorks_sourceArchiveId_sourceArchiveType",
                table: "WorkGroups");

            migrationBuilder.DropIndex(
                name: "IX_WorkGroups_sourceArchiveId_sourceArchiveType",
                table: "WorkGroups");

            migrationBuilder.DropColumn(
                name: "sourceArchiveId",
                table: "WorkGroups");

            migrationBuilder.DropColumn(
                name: "sourceArchiveType",
                table: "WorkGroups");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
    }
}
