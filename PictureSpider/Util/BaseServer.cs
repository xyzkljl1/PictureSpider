using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PictureSpider.Util
{
    public class BaseServer
    {
        //Init应当在构建之后，使用之前，在主线程(UI线程)中调用并等待完成
        public virtual async Task Init() { }
    }
}
