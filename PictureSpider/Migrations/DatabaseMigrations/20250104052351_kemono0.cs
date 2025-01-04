using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PictureSpider.Migrations.DatabaseMigrations
{
    /// <inheritdoc />
    public partial class kemono0 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    id = table.Column<string>(type: "varchar(95)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    service = table.Column<string>(type: "varchar(95)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    dowloadExternalWorks = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    dowloadWorks = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    displayId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    displayText = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    followed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    queued = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => new { x.id, x.service });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "WorkGroups",
                columns: table => new
                {
                    id = table.Column<string>(type: "varchar(95)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    userservice = table.Column<string>(type: "varchar(95)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    title = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    desc = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    readed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    fav = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    fetched = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    userid = table.Column<string>(type: "varchar(95)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkGroups", x => new { x.id, x.userservice });
                    table.ForeignKey(
                        name: "FK_WorkGroups_Users_userid_userservice",
                        columns: x => new { x.userid, x.userservice },
                        principalTable: "Users",
                        principalColumns: new[] { "id", "service" });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ExternalWorks",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false),
                    workGroupuserservice = table.Column<string>(type: "varchar(95)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    urlPath = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    urlHost = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    index = table.Column<int>(type: "int", nullable: false),
                    excluded = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    workGroupid = table.Column<string>(type: "varchar(95)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalWorks", x => new { x.id, x.workGroupuserservice });
                    table.ForeignKey(
                        name: "FK_ExternalWorks_WorkGroups_workGroupid_workGroupuserservice",
                        columns: x => new { x.workGroupid, x.workGroupuserservice },
                        principalTable: "WorkGroups",
                        principalColumns: new[] { "id", "userservice" });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Works",
                columns: table => new
                {
                    service = table.Column<string>(type: "varchar(95)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    urlPath = table.Column<string>(type: "varchar(95)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    urlHost = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    index = table.Column<int>(type: "int", nullable: false),
                    excluded = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    workGroupid = table.Column<string>(type: "varchar(95)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    workGroupuserservice = table.Column<string>(type: "varchar(95)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    coverGroupid = table.Column<string>(type: "varchar(95)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    coverGroupuserservice = table.Column<string>(type: "varchar(95)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Works", x => new { x.urlPath, x.service });
                    table.ForeignKey(
                        name: "FK_Works_WorkGroups_coverGroupid_coverGroupuserservice",
                        columns: x => new { x.coverGroupid, x.coverGroupuserservice },
                        principalTable: "WorkGroups",
                        principalColumns: new[] { "id", "userservice" });
                    table.ForeignKey(
                        name: "FK_Works_WorkGroups_workGroupid_workGroupuserservice",
                        columns: x => new { x.workGroupid, x.workGroupuserservice },
                        principalTable: "WorkGroups",
                        principalColumns: new[] { "id", "userservice" },
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalWorks_workGroupid_workGroupuserservice",
                table: "ExternalWorks",
                columns: new[] { "workGroupid", "workGroupuserservice" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkGroups_userid_userservice",
                table: "WorkGroups",
                columns: new[] { "userid", "userservice" });

            migrationBuilder.CreateIndex(
                name: "IX_Works_coverGroupid_coverGroupuserservice",
                table: "Works",
                columns: new[] { "coverGroupid", "coverGroupuserservice" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Works_workGroupid_workGroupuserservice",
                table: "Works",
                columns: new[] { "workGroupid", "workGroupuserservice" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalWorks");

            migrationBuilder.DropTable(
                name: "Works");

            migrationBuilder.DropTable(
                name: "WorkGroups");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
