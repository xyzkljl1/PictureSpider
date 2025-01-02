using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PictureSpider.Migrations.DatabaseMigrations
{
    /// <inheritdoc />
    public partial class lsf_dev0 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Waited",
                columns: table => new
                {
                    path_raw = table.Column<byte[]>(type: "varbinary(767)", nullable: false),
                    sub_path_raw = table.Column<byte[]>(type: "longblob", nullable: true),
                    date = table.Column<DateTime>(type: "datetime", nullable: false),
                    fav = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Waited", x => x.path_raw);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Waited");
        }
    }
}
