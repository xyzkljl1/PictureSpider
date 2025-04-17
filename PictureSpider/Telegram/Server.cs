using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.ClearScript.JavaScript;
using Mysqlx;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using PictureSpider;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TdLib;
using TdLib.Bindings;
using Windows.Media.Playback;
using Windows.Web.Http;
using static TdLib.TdApi;
using static TdLib.TdApi.ChatList;
using static TdLib.TdApi.ChatType;
using static TdLib.TdApi.MessageContent;
using static TdLib.TdApi.SearchMessagesFilter;
using static TdLib.TdApi.TopChatCategory;

/*
 * 和其它server不同，该server仅负责下载，没有浏览队列，也不记录图片的read/fav等信息
 * 所有文件下载到本地后通过localsinglefile浏览
 */
namespace PictureSpider.Telegram
{
    /*
     * 使用tdlib api的C#封装
     * 从https://my.telegram.org/auth获得api_id
     * https://github.com/egramtel/tdsharp
     * https://github.com/tdlib/td
     * 申请app key:
     * https://my.telegram.org/auth ->api dev tools
     * 很多代理都会报错，并且一天只能登录几次，非常坑爹
     * 使用免费代理 https://www.lumiproxy.com/zh-hans/online-proxy/proxysite/尝试后注册成功
     */
    using HttpClient = System.Net.Http.HttpClient;
    using HttpResponseMessage = System.Net.Http.HttpResponseMessage;
    using HttpStatusCode = System.Net.HttpStatusCode;

    public partial class Server : BaseServer, IDisposable
    {
        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();
        private string download_dir_root;
        private string download_dir_organized;
        private string download_dir_other;
        private string download_dir_tmp;
        //private string request_proxy;
        private string phone_number = "";
        private int apiId = 0;
        private string apiHash = "";
        private string myDownloadServerAddr = "";
        private TdClient tgClient;
        private Database database;
        private HttpClient localHttpClient;
        //private static readonly ManualResetEventSlim ReadyToAuthenticate = new();
        public Server(Config config)
        {
            base.logPrefix = "G";
            tgClient = new TdClient();
            //这破玩意输出的log太多了，直接关了
            //但是执行完之前还会输出一段日志到error，怎么解决？
            tgClient.Execute(new TdApi.SetLogVerbosityLevel { NewVerbosityLevel = 0 });

            myDownloadServerAddr=config.MyDownloadServerAddress;
            apiId = config.TelegramApiID;
            apiHash = config.TelegramApiHash;
            phone_number = config.TelegramPhoneNumber;
            download_dir_root = config.TelegramDownloadDir;
            download_dir_organized = Path.Combine(download_dir_root, "org");
            download_dir_other = Path.Combine(download_dir_root, "other");
            download_dir_tmp = Path.Combine(download_dir_root, "tmp");
            foreach (var dir in new List<string> { download_dir_root, download_dir_organized, download_dir_tmp, download_dir_other })
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

            database = new Database(config.TelegramConnectStr);

            var handler = new HttpClientHandler()
            {
                MaxConnectionsPerServer = 256,
                UseCookies = false,
            };
            handler.ServerCertificateCustomValidationCallback = delegate { return true; };
            localHttpClient = new HttpClient(handler);
        }
#pragma warning disable CS0162 // 检测到无法访问的代码
        public override async Task Init() {
#if DEBUG
            return;
#endif
            try
            {
                var ok =await tgClient.SetTdlibParametersAsync(apiId:apiId,apiHash:apiHash, systemLanguageCode: "zh-hans", deviceModel:"Desktop",applicationVersion:"5.6.2",
                                useChatInfoDatabase:true,useFileDatabase:true,useMessageDatabase:true,useSecretChats:true);
                //似乎只有重新登录的时候需要设置代理？其它时候不需要？
                //var proxy = await tgClient.AddProxyAsync("127.0.0.1", 1195, true, new TdApi.ProxyType.ProxyTypeSocks5());
                if (!await Login())
                {
                    LogError("Stop Init because login failed");
                    return;
                }
            }
            catch (Exception ex) 
            {
                LogError($"Fail to Init :{ex.Message}");
                return;
            }
            Log("Init Done.Start Schedule");
#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
            RunSchedule();
#pragma warning restore CS4014 
            //Task.Run(RunSchedule);
        }
#pragma warning restore CS0162
        public async Task<bool> Login()
        {
            var loginState = await tgClient.GetAuthorizationStateAsync();
            //登录过一次后会自动记住，下次启动无需登录
            if(loginState.DataType!= "authorizationStateReady")
            {
                //login,似乎不需要密码或验证码？
                await tgClient.SetAuthenticationPhoneNumberAsync(phone_number);
                loginState = await tgClient.GetAuthorizationStateAsync();
                if(loginState.DataType== "authorizationStateWaitCode")
                {
                    AllocConsole();
                    var code=Console.ReadLine();
                    await tgClient.CheckAuthenticationCodeAsync(code);
                    //await tgClient.CheckAuthenticationCodeAsync("78164");
                    LogError("Need Input Auth Code Manually");
                    loginState = await tgClient.GetAuthorizationStateAsync();
                    if (loginState.DataType != "authorizationStateReady")
                        LogError($"Login With Code Fail: {loginState.DataType}");
                    else
                        Log("Login by code with phone number");
                }
                else if (loginState.DataType != "authorizationStateReady")
                {
                    LogError($"Login Fail: {loginState.DataType}");
                    return false;
                }
                else
                    Log("Login with phone number");
            }
            return true;
        }
        //更新channel列表
        public async Task FetchChatList()
        {
            //https://neliosoftware.com/content/help/how-do-i-get-the-channel-id-in-telegram/
            //https://gist.github.com/mraaroncruz/e76d19f7d61d59419002db54030ebe35?permalink_comment_id=4457233
            //获取chat(包含channel等)列表
            //所有chat必须先通过GetChatsAsync获取chat列表后，才能获取相应信息，否则会返回chat not found
            //user则需要GetUserAsync
            //！！！！暂不考虑chat数量超过1000的情况
            var chatListMain = await tgClient.GetChatsAsync(new ChatListMain(), 1000);
            var chatListArchive = await tgClient.GetChatsAsync(new ChatListArchive(), 1000);
            int ct = 0;
            foreach(var chatIds in new long[][] { chatListMain.ChatIds , chatListArchive.ChatIds})
                foreach(var chatId in chatIds)
                {
                    try
                    {
                        var chatInfo = await tgClient.GetChatAsync(chatId);
                        var chatType = chatInfo.Type as ChatTypeSupergroup;
                        if (chatType==null)
                            continue;
                        var supergroup=await tgClient.GetSupergroupAsync(chatType.SupergroupId);
                        var channel=database.Channels.FirstOrDefault(x => x.id == chatId);
                        if(channel == null)
                        {
                            channel = new Channel();
                            database.Channels.Add(channel);
                        }
                        channel.title = chatInfo.Title;
                        channel.id = chatInfo.Id;
                        if(supergroup.Usernames!=null)
                            channel.username = supergroup.Usernames.EditableUsername;
                        ct++;
                    }
                    catch (Exception ex)//如果某个chat获取失败，忽略
                    {
                        LogError($"Can't Get Chat {chatId} Info :{ex.Message}");
                    }
                }
            database.SaveChanges();
            Log($"Fetch {ct} Chats.");
        }
        public async Task FetchMessageList()
        {
            Log("Start Fetch Message List");
            foreach (var channel in database.Channels.ToList())//要tolist获得一份拷贝，否则database会处于占用中
            {
                if (!(channel.download_telegraph || channel.download_video || channel.download_illust || channel.download_comments))
                    continue;
                int ct = 0;
                try
                {
                    var channelInfo = await tgClient.GetChatAsync(channel.id);
                    var cursor = channelInfo.LastMessage;
                    if (cursor is null)
                        continue;
                    int last_timestamp = -1;
                    channel.end_timestamp = Math.Max(channel.start_timestamp, channel.end_timestamp);
                    last_timestamp = cursor.Date;
                    if (cursor.Date > last_timestamp)
                        ct += database.AddOrIgnoreMessage(cursor) ? 1 : 0;
                    while (cursor != null && cursor.Date > channel.end_timestamp)
                    {
                        //获取不包含cursor的，cursor之前的若干message,按id倒序排列
                        var chunk = await tgClient.GetChatHistoryAsync(channel.id, fromMessageId: cursor.Id, limit: 500);
                        if (chunk is null || chunk.Messages_.Count() < 1) break;
                        foreach (var messageInfo in chunk.Messages_)
                            ct += database.AddOrIgnoreMessage(messageInfo) ? 1 : 0;
                        cursor = chunk.Messages_.Last();
                    }
                    channel.end_timestamp = last_timestamp;
                    Log($"Update {channel.title} end_timestamp to {channel.end_timestamp}");
                }
                catch (Exception ex)
                {
                    LogError($"Fail to get message list {channel.id}: {ex.Message}");
                }
                database.SaveChanges();
                Log($"Fetch {ct} Messages from {channel.title}");
            }
        }
        public async Task<string> GetAlbumCaptionText(TdApi.Message messageInfo,int length_limit=30)
        {
            //一组图中只有一个有captain文本
            //升序排列，一般文本在id最小的message上
            foreach (var _m in database.Messages.Where(ele => ele.albumid == messageInfo.MediaAlbumId).OrderBy(x=>x.id).ToList())
            {
                var info = await tgClient.GetMessageAsync(_m.chat, _m.id);
                var content = info.Content;
                var fieldInfo = content.GetType().GetProperty("Caption", System.Reflection.BindingFlags.Public| System.Reflection.BindingFlags.Instance);
                if(fieldInfo!=null)
                {
                    var captionText=fieldInfo.GetValue(content) as FormattedText;
                    if (captionText != null&&captionText.Text!=null&&captionText.Text!="")
                    {
                        var ret=captionText.Text;
                        if(ret.Length>length_limit)
                            ret=ret.Substring(0, length_limit);
                        Util.ReplaceInvalidCharInFilename(ref ret);
                        ret = ret.Replace("[", "").Replace("]", "");
                        ret = ret.Trim(new char[]{ ' ','#'});//目录名末尾有空格似乎会令Directory.CreateDir创建的目录不正确？？ 并去掉tag的#
                        return ret;
                    }
                }
            }
            return "";
        }
        public async Task DownloadByMessages()
        { 
            Log("Start Download Try");
            foreach (var channel in database.Channels.ToList())//要tolist获得一份拷贝，否则database会处于占用中
            {
                if (!(channel.download_telegraph || channel.download_video || channel.download_illust || channel.download_comments))
                    continue;
                //if (channel.id != -1002404835607)
                //    continue;
                int ct = 0;
                int ct2 = 0;
                var messages = database.Messages.Where(message => message.state == MessageState.Wait && message.chat == channel.id).ToList();
                //var messages = database.Messages.Where(message => message.chat == channel.id&&message.timestamp>= 1728880449).ToList();
                foreach (var message in messages)
                    if(message.state == MessageState.Wait)//有的下载会改变其它message的状态，此处还要再判断一次state
                    {
                        try
                        {
                            TdApi.Message messageInfo;
                            try
                            {
                                messageInfo = await tgClient.GetMessageAsync(message.chat, message.id);
                            }
                            catch (TdLib.TdException e)
                            {
                                if(e.Error.Code==404&&e.Error.Message=="Not Found")//not found
                                {
                                    message.state = MessageState.NotFound;
                                    database.SaveChanges();
                                    Log($"Drop Message {message.id} for not found");
                                    continue;
                                }
                                throw;
                            }
                            if (messageInfo.Content == null) continue;
                            if (channel.download_telegraph)
                            {
                                var content=messageInfo.Content as MessageText;
                                if (content != null&& content.WebPage!=null&&content.WebPage.Url.Contains("https://telegra.ph"))
                                {
                                    if (database.FinishedTasks.FirstOrDefault(ele=>ele.url==content.WebPage.Url) != null)//查重
                                    {
                                        message.state = MessageState.Dup;
                                    }
                                    else if (await RequestGetAsync($"{myDownloadServerAddr}/?url={System.Web.HttpUtility.UrlEncode(content.WebPage.Url)}"))
                                    {
                                        message.state = MessageState.Done;
                                        Log($"Send Task to MyDownload Server:{message.id} / {content.WebPage.Title} / {content.WebPage.Url}");
                                        database.FinishedTasks.Add(new FinishedTask { url=content.WebPage.Url , title=content.WebPage.Title ,comment=$"Download telegraph from {message.id} in {channel.title} at {DateTime.Now}"});
                                        ct++;
                                    }
                                    database.SaveChanges();
                                }
                            }
                            else if (channel.download_illust||channel.download_comments)
                            {
                                //download_comments:
                                //有的频道发图包，是发一组图，然后在评论里补上其余的，评论里的message实际是在另一个chat里的对第一组图的评论
                                //直接从源chat中获取图片，假定源chat不允许讨论，只包含有效信息
                                //此时操作类似download_illust,每张图分别下载，只是存储路径不同

                                if ((messageInfo.Content as MessagePhoto) is null) continue;
                                //如果这一组图片有文字描述
                                var filename_prefix = "";
                                var parent_dir = "";
                                var comment_title = "";
                                if(channel.download_illust)//单张下载，放到channel目录下
                                {
                                    comment_title=filename_prefix = await GetAlbumCaptionText(messageInfo);
                                    filename_prefix += $"_{message.id}";
                                    parent_dir = Path.Combine(download_dir_other, $"{channel.id}");//考虑到title可能会变，不用title作目录名
                                }
                                else//套图，放到dir_org下以caption命名
                                {
                                    filename_prefix = $"{message.id}";
                                    var cursor = messageInfo;//根据replyto找到第一条的message，放到第一条message的目录
                                    while (cursor != null && cursor.ReplyTo != null)
                                    {
                                        var replyto = cursor.ReplyTo as MessageReplyTo.MessageReplyToMessage;
                                        if (replyto is null)
                                            break;
                                        cursor = await tgClient.GetMessageAsync(replyto.ChatId, replyto.MessageId);
                                    }
                                    if (cursor is null)
                                        continue;
                                    var album_title = await GetAlbumCaptionText(cursor);
                                    comment_title=album_title;

                                    if (album_title != "")
                                        parent_dir = Path.Combine(download_dir_organized, album_title);
                                    else//找不到描述的视同散图
                                        parent_dir = Path.Combine(download_dir_other, $"{channel.id}");
                                }
                                if (!Directory.Exists(parent_dir)) 
                                    Directory.CreateDirectory(parent_dir);
                                var content = messageInfo.Content as MessagePhoto;
                                var file = content.Photo.Sizes.Last().Photo;
                                //https://github.com/tdlib/td/issues/1025
                                //注意unique id不是unique的,不同文件可以有同一个unique id,但是同一个文件只会有一个unique id且不会随时间改变，类似md5
                                //file.id是动态的，每次启动都会改变，在一次tdlib运行期间一个文件只会有一个file.id
                                //一个文件可以有多个file.remote.id，不同文件不会有相同的file.remote.id, 即使在一次运行时也可能从一个file获取到不同的file.remote.id，但是只要文件仍然可以访问，之前可用的id就仍然可用
                                //真tm坑爹                                
                                //只好使用file.remote.id判重，但是这样一个文件仍然可能下载多次
                                //由于remote.id不会失效,推测一个文件只会有有限个remote.id,期待每个都下载过一次后就不会重复下载
                                var fileRemoteId = file.Remote.Id;
                                if (fileRemoteId is not null&&fileRemoteId!="")//注意id可能为空
                                {
                                    if(database.FinishedTasks.FirstOrDefault(ele => ele.fileid == fileRemoteId) != null)//重复
                                        message.state = MessageState.Dup;
                                    else
                                    {
                                        //原地等待到下载完成
                                        //TODO:多线程？
                                        var downloadedFile=await tgClient.DownloadFileAsync(fileId:file.Id,priority:32,synchronous:true);
                                        if (downloadedFile.Local.IsDownloadingCompleted)
                                        {
                                            var filename = $"{filename_prefix}_{Path.GetFileName(downloadedFile.Local.Path)}";
                                            Util.ReplaceInvalidCharInFilename(ref filename);
                                            var target_path = Path.Combine(parent_dir, filename);
                                            if (System.IO.File.Exists(target_path))//覆盖旧的
                                                System.IO.File.Delete(target_path);
                                            System.IO.File.Move(downloadedFile.Local.Path,Path.Combine(parent_dir, filename));
                                            //修改数据库要放在移动文件后面以防移动文件失败
                                            database.FinishedTasks.Add(new FinishedTask { title = comment_title,fileid=fileRemoteId, 
                                                comment = channel.download_illust?
                                                    $"Download single illust from {message.id} in {channel.title} at {DateTime.Now}: {filename}" :
                                                    $"Download illust of comment set from {message.id} in {channel.title} at {DateTime.Now}: {filename}"
                                                    });
                                            message.state = MessageState.Done;
                                            ct++;
                                        }
                                        else
                                            LogError($"Download Fail On {message.id}");
                                    }
                                }
                                else//不明
                                {
                                    LogError($"Empty File ID {message.id}");
                                }
                                database.SaveChanges();
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"Fail to process Message {message.id}:{ex.Message} from {channel.username}({channel.id})");
                        }
                    }
                database.SaveChanges();
                if (ct > 0)
                    Log($"Process {ct} Download Request in {channel.title}");
                if (ct2 > 0)
                    Log($"Ignore {ct} Message in {channel.title}");
            }
        }
        public async Task<bool> RequestGetAsync(string url)
        {
            try
            {
                using (HttpResponseMessage response = await localHttpClient.GetAsync(url))
                    if (response.StatusCode == HttpStatusCode.OK)
                        return true;
                    else
                        LogError($"Http Get Fail:{url}");
            }
            catch (Exception e)
            {
                LogError($"Http Exception:{e.Message}");
            }
            return false;
        }
        private async Task RunSchedule()
        {
            var interval = new TimeSpan(1, 0, 0);
            TimeSpan daily_interval = new TimeSpan(25, 0, 0);
            TimeSpan weekly_interval = new TimeSpan(0, 0, 0);            
            await FetchChatList();//需要最开始调用一次GetChatsAsync,否则后面获取不到chat具体信息
            do
            {
                if (weekly_interval.TotalDays >= 7)//weekly task
                {
                    await FetchChatList();
                    weekly_interval = new TimeSpan(0, 0, 0);
                }
                if (daily_interval.TotalDays >= 1)//daily task
                {
                    await FetchMessageList();
                    await DownloadByMessages();
                    daily_interval = new TimeSpan(0, 0, 0);
                    Log("Daily Task Done");
                }
                daily_interval += interval;
                weekly_interval += interval;
                await Task.Delay(interval);
            }
            while (true);
        }
        public void Dispose()
        {
            localHttpClient.Dispose();
            tgClient.Dispose();
        }
    }
}
