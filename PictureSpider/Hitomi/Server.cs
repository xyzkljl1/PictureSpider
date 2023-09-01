using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using PictureSpider;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace PictureSpider.Hitomi
{
    public partial class Server : BaseServer, IDisposable
    {
        public Database database;
        private HttpClient httpClient;

        public Server(Config config)
        {
            database = new Database(config.HitomiConnectStr);
            //database.Illusts.Add(new Illust());
            //database.SaveChanges();
        }
        public void Dispose()
        {
        }
        public override async Task Init()
        {
        }
        public override BaseUser GetUserById(string id)
        {
            return null;
        }
    }
}
