using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PictureSpider.Migrations
{
    /// <inheritdoc />
    public partial class k3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "dowloadImageWorks",
                table: "Users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "dowloadImageWorks",
                table: "Users");
        }
    }
}
