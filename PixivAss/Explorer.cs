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
        private const int cache_size = 30;
        private List<ImageCache> cache_pool = new List<ImageCache>();
        private List<Illust> illust_list = new List<Illust>();
        private Image empty_image;
        private int index = 0;
        private int sub_index = 0;
        public Client pixivClient;

        private Label title_label;
        private Label page_label;
        public Explorer()
        {
            this.SizeMode = PictureBoxSizeMode.Zoom;
        }
        public void SetLabel(Label _title,Label _page)
        {
            title_label = _title;
            page_label = _page;
        }
        public void SetClient(Client _client)
        {
            pixivClient = _client;
            string path = pixivClient.special_dir + "/" + "empty_pic.png";
            if (File.Exists(path))
                empty_image = Image.FromFile(path);
            else
                empty_image = new Bitmap(1, 1);
        }
        public void SetAllIllust()
        {
            this.Image = null;
            cache_pool.Clear();
            illust_list.Clear();
            foreach (var illust in pixivClient.database.GetAllIllustFull())
                if (illust.bookmarked && illust.bookmarkPrivate == false)
                    illust_list.Add(illust);
            SlideTo(0,0);
        }
        private void SlideTo(int i,int j)
        {
            if (i >= illust_list.Count || i < 0)
                return;
            Illust illust = illust_list[i];
            ImageCache cache = Load(illust);
            if (j < cache.data.Count)
                this.Image = cache.data[j];
            else
                this.Image = empty_image;
            index = i;
            sub_index = j;

            title_label.Text = String.Format("{0}\n{1}",illust.title,illust.description);
            page_label.Text = String.Format("{0}/{1}",sub_index+1,illust.pageCount);

            for (int idx = i - 5; idx < i + 5; ++idx)
                if (idx >= 0 && idx <= illust_list.Count)
                    Load(illust_list[idx]);
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
                SlideTo(new_index,0);
        }
        private void SlideVertical(int i)
        {
            int new_index = sub_index + i;
            if (new_index >= 0 && new_index < illust_list[index].pageCount)
                SlideTo(index,new_index);
        }

        public void SlideRight(object sender,EventArgs args)
        {
            SlideHorizon(1);
        }
        public void SlideLeft(object sender, EventArgs args)
        {
            SlideHorizon(-1);
        }
        public void SlideUp(object sender, EventArgs args)
        {
            SlideVertical(-1);
        }
        public void SlideDown(object sender, EventArgs args)
        {
            SlideVertical(1);
        }
        public void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left)
                SlideHorizon(-1);
            else if (e.KeyCode == Keys.Right)
                SlideHorizon(1);
            else if (e.KeyCode == Keys.Up)
                SlideVertical(-1);
            else if (e.KeyCode == Keys.Down)
                SlideVertical(1);

        }
    }
}
