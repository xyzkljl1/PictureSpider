using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PictureSpider.Migrations.Twitter
{
    [DbContext(typeof(PictureSpider.Twitter.Database))]
    [Migration("20260530143000_AddTwitterPendingUiOperations")]
    public partial class AddTwitterPendingUiOperations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS `PendingUiOperations` (
                `Id` bigint NOT NULL AUTO_INCREMENT,
                `CreatedAt` datetime(6) NOT NULL,
                `Kind` int NOT NULL,
                `TargetKey` varchar(128) NOT NULL,
                `Value` int NOT NULL,
                PRIMARY KEY (`Id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingUiOperations");
        }
    }
}
