using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PixivAss.Data
{
    class IllustGroup
    {
        public string id;//
        public string title;
        public int illustType;//?
        public int xRestrict;//!pub
        //public int restrict;//?
        //public int sl;//? int
        //public string description;//? usually empty
        //public string thumbUrl;
        //public List<String> tags;
        public string userId;
        //public string userName;//redun
        public int width;
        public int height;
        public int pageCount;
        //public Boolean isBookmarkable;//redun
        public string bookmarkDataId;
        public Boolean bookmarkDataPrivate;
        //public string profileImageUrl;//?
    }
}
