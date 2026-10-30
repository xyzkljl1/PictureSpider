using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PictureSpider.Migrations.Telegram
{
    [DbContext(typeof(PictureSpider.Telegram.Database))]
    [Migration("20261030000000_AddTelegramMessageChatStateIndex")]
    public partial class AddTelegramMessageChatStateIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Messages_chat_state",
                table: "Messages",
                columns: new[] { "chat", "state" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_chat_state",
                table: "Messages");
        }
    }
}
