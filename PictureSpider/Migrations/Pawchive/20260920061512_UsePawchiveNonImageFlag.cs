using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PictureSpider.Migrations.Pawchive
{
    /// <inheritdoc />
    public partial class UsePawchiveNonImageFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "isNonImage",
                table: "WorkGroups",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE `WorkGroups` SET `isNonImage` = (`type` = 'NonImages')");

            migrationBuilder.DropColumn(
                name: "type",
                table: "WorkGroups");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "WorkGroups",
                type: "enum('Images','NonImages')",
                nullable: false,
                defaultValue: "Images")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql("UPDATE `WorkGroups` SET `type` = CASE WHEN `isNonImage` THEN 'NonImages' ELSE 'Images' END");

            migrationBuilder.DropColumn(
                name: "isNonImage",
                table: "WorkGroups");
        }
    }
}
