using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PictureSpider.Pawchive;

namespace PictureSpider.Migrations.Pawchive
{
    [DbContext(typeof(Database))]
    [Migration("20261124000000_AddPawchivePublishedAt")]
    public class AddPawchivePublishedAt : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(name: "PublishedAt", table: "WorkGroups", type: "datetime(6)", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PublishedAt", table: "WorkGroups");
        }
    }
}
