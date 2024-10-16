using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PictureSpider.Telegram
{
    public enum MessageState
    {
        Ignore=0,
        Wait=1,
        Done=2,
        Dup=3,
        NotFound=4,
    };
    public enum DownloadType
    {
        Default,
        IllustGroupFromTelegraph,
        IllustGroupFromComments,
        SingleIllust,
    };
    //Message
    [Table("Messages")]
    [PrimaryKey(nameof(id), nameof(chat))]
    [Index(nameof(albumid))]
    public class Message
    {
        //注意id并非唯一，id+chatId才是唯一的
        public long id { get; set; }
        public long chat { get; set; } = 0;
        public int timestamp { get; set; } = 0;
        public long albumid { get; set; } = 0;
        public MessageState state { get; set; }= MessageState.Wait;
        //最初下载时的本地路径，仅供参考，由于该server不负责管理本地文件，并不保证本地文件存在
        [StringLength(600)]
        public string localPath { get; set; }= "";
        public DownloadType downloadType { get; set; }= DownloadType.Default;
        //message的json，备用,longtext
        public string json { get; set; } = "";
        public Message() { }
    }
    //实际包含了Channel和Chat
    [Table("Channels")]
    public class Channel
    {
        [Key]
        public long id { get; set; }
        [StringLength(600)]
        public string title { get; set; } = "";
        [StringLength(600)]
        public string username { get; set; } = "";
        public int start_timestamp { get; set; } = 0;
        public int end_timestamp { get; set; } = 0;
        //下载telegraph形式的套图
        public bool download_telegraph { get; set; } = false;
        //下载comment里的套图
        public bool download_comments { get; set; } = false;
        //散图形式下载
        public bool download_illust { get; set; } = false;
        //视频
        public bool download_video { get; set; } = false;
        public Channel() { }
    }
    [Table("FinishedTasks")]
    [Index(nameof(fileid))]
    [Index(nameof(title))]
    [Index(nameof(url))]
    public class FinishedTask
    {
        [Key]
        public long id { get; set; }
        [StringLength(400)]
        public string title { get; set; } = "";
        [StringLength(600)]
        public string url { get; set; } = "";
        public string comment { get; set; } = "";
        [StringLength(100)]
        public string fileid { get; set; } = "";
        public FinishedTask() { }
    }
}
