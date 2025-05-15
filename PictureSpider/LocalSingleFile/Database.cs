using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
namespace PictureSpider.LocalSingleFile
{
    public class Database : BaseEFDatabase
    {
        public DbSet<Illust> Waited { get; set; }
    }
}
