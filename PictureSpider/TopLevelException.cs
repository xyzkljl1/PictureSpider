using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PictureSpider
{
    //其实就是MyException
    class TopLevelException(string message) : Exception(message);
}
