using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using PixivAss.Data;
using System.Linq;
using System.IO;

namespace PixivAss
{
    class Explorer : PictureBox
    {
        class ImageCache
        {
            public Illust illust;
            public List<Image> data;
            public DateTime required_time;
            public bool is_using = false;
        }
        private const int cache_size = 20;
        private List<ImageCache> cache_pool = new List<ImageCache>();
        private List<Illust> illust_list = new List<Illust>();
        private Image empty_image;
        private int index = 0;
        Client pixivClient;
        public Explorer()
        {
            this.SizeMode = PictureBoxSizeMode.Zoom;
        }
        public void SetClient(Client _client)
        {
            pixivClient = _client;
            string path = pixivClient.special_dir + "/" + "empty_pic.png";
            if (File.Exists(path))
                empty_image = Image.FromFile(path);
            else
                empty_image = new Bitmap(1, 1);
            SetAllIllust();
        }
        private void SetAllIllust()
        {
            this.Image = null;
            cache_pool.Clear();
            illust_list.Clear();
            foreach (var illust in pixivClient.database.GetAllIllustFull())
                if (illust.bookmarked && illust.bookmarkPrivate == false)
                    illust_list.Add(illust);
            SlideTo(0);
        }
        private void SlideTo(int i)
        {
            if (i >= illust_list.Count || i < 0)
                return;
            Illust illust = illust_list[i];
            ImageCache cache = Load(illust);
            this.Image = cache.data[0];
            index = i;
        }
        private ImageCache Load(Illust illust)
        {
            foreach (var cache in cache_pool)
                if (cache.illust.id == illust.id)//hit
                {
                    cache.required_time = DateTime.UtcNow;
                    return cache;
                }
            {//not hit
                ImageCache cache = new ImageCache();
                cache.illust = illust;
                cache.required_time = DateTime.UtcNow;
                cache.data = new List<Image>();
                for (int i = 0; i < illust.pageCount; i++)
                {
                    string path = pixivClient.GetDownloadDir(illust) + "/" + pixivClient.GetDownloadFileName(illust, i);
                    if (File.Exists(path))
                        cache.data.Add(Image.FromFile(path));
                    else
                        cache.data.Add(empty_image);
                }
                cache_pool.Add(cache);
                while (cache_pool.Count > cache_size)
                {
                    ImageCache min = new ImageCache();
                    min.required_time = DateTime.UtcNow;
                    foreach (var item in cache_pool)
                        if (item.required_time < min.required_time)
                            min = item;
                    cache_pool.Remove(min);
                }
                return cache;
            }
        }
        private void SlideHorizon(int i)
        {
            int new_index = index + i;
            if (new_index >= 0 && new_index < illust_list.Count)
                SlideTo(new_index);
        }

        public void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left)
                SlideHorizon(-1);
            else if (e.KeyCode == Keys.Right)
                SlideHorizon(1);
        }
    }
}
