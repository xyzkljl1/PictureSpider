using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
namespace PictureSpider.Telegram
{
    public class Database : BaseEFDatabase
    {
        public bool AddOrIgnoreMessage(TdLib.TdApi.Message messageInfo)
        {
            var message = Messages.FirstOrDefault(ele => ele.id == messageInfo.Id&&ele.chat==messageInfo.ChatId);
            if (message is null)
            {
                message = new Message { id = messageInfo.Id, chat = messageInfo.ChatId, timestamp = messageInfo.Date,albumid=messageInfo.MediaAlbumId, json = JsonConvert.SerializeObject(messageInfo) };
                Messages.Add(message);
                return true;
            }
            return false;
        }
        public DbSet<Channel> Channels { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<FinishedTask> FinishedTasks { get; set; }
    }
}
