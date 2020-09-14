using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using PixivAss.Data;
using System.Linq;
using System.IO;
using System.ComponentModel;

namespace PixivAss
{
    class Explorer : PictureBox, INotifyPropertyChanged
    {
        class ImageCache
        {
            public Illust illust;
            public List<Image> data;
            public DateTime required_time;
        }
        private const int cache_size = 30;
        private List<ImageCache> cache_pool = new List<ImageCache>();
        private List<Illust> illust_list = new List<Illust>();
        private int index = 0;
        private int sub_index = 0;
        private Image empty_image;
        public Client pixivClient;        
        private Timer timer = new Timer();
        private bool playing = false;

        public event PropertyChangedEventHandler PropertyChanged=delegate { };
        [Bindable(true)]
        public string IndexText{get { return illust_list.Count>0? (sub_index+1).ToString()+"/"+ illust_list[index].pageCount.ToString():""; }}
        [Bindable(true)]
        public string DescText{get { return illust_list.Count > 0 ? illust_list[index].title + "</br>" + illust_list[index].description : "";} }
        [Bindable(true)]
        public string TotalPageText{get { return (playing?"Pause":"Play")+"\n"+(illust_list.Count > 0 ? (index+1).ToString()+"/"+illust_list.Count.ToString() : ""); }}
        [Bindable(true)]
        public string IdText {get { return (illust_list.Count > 0 ? "pid="+illust_list[index].id : "None");}}
        [Bindable(true)]
        public string TagText{get { return illust_list.Count > 0 ? String.Join("\n", illust_list[index].tags) : ""; }}
        [Bindable(true)]
        public Bitmap FavIcon
        {
            get
            {
                if (illust_list.Count == 0)
                    return Properties.Resources.NotFav;
                if (illust_list[index].bookmarked)
                    return illust_list[index].bookmarkPrivate ? Properties.Resources.FavPrivate : Properties.Resources.Fav;
                return Properties.Resources.NotFav;
            }
        }

        public Explorer()
        {
            this.SizeMode = PictureBoxSizeMode.Zoom;
            timer.Tick += new EventHandler(SlideRight);
            timer.Interval = 1000;
        }
        //设置client,构造函数的调用是自动生成的不知道怎么塞进去，只能独立出来了
        public void SetClient(Client _client)
        {
            pixivClient = _client;
            string path = pixivClient.special_dir + "/" + "empty_pic.png";
            if (File.Exists(path))
                empty_image = Image.FromFile(path);
            else
                empty_image = new Bitmap(1, 1);
        }
        //通知界面刷新
        public void UpdateUI()
        {
            PropertyChanged(this, new PropertyChangedEventArgs("IndexText"));
            PropertyChanged(this, new PropertyChangedEventArgs("DescText"));
            PropertyChanged(this, new PropertyChangedEventArgs("TotalPageText"));
            PropertyChanged(this, new PropertyChangedEventArgs("IdText"));
            PropertyChanged(this, new PropertyChangedEventArgs("FavIcon"));
            PropertyChanged(this, new PropertyChangedEventArgs("TagText"));
        }
        //播放
        public void Play()
        {
            if (playing)
                timer.Stop();
            else
                timer.Start();
            playing = !playing;
            UpdateUI();
        }
        //设置当前列表
        public void SetList(bool fav,bool fav_private,bool queue)
        {
            this.Image = null;
            cache_pool.Clear();
            illust_list.Clear();
            foreach (var illust in pixivClient.database.GetAllIllustFull())
            {
                bool pass = false;
                if (fav && (illust.bookmarked && !illust.bookmarkPrivate))
                    pass = true;
                else if (fav_private && (illust.bookmarked && illust.bookmarkPrivate))
                    pass = true;
                else if (queue && (!illust.readed) && !illust.bookmarked)
                    pass = true;
                if(pass&&!illust.valid)
                {
                    pass = false;
                    for (int i = 0; i < illust.pageCount; i++)
                        if (File.Exists(pixivClient.GetDownloadDir(illust) + "/" + Client.GetDownloadFileName(illust, i)))
                        {
                            pass = true;
                            break;
                        }
                }
                if (pass)
                    illust_list.Add(illust);
            }
            SlideTo(0,0);
        }
        //载入某个Illust的全部图片
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
                    string path = pixivClient.GetDownloadDir(illust) + "/" + Client.GetDownloadFileName(illust, i);
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
        //切图
        private void SlideTo(int i, int j)
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
            UpdateUI();
            for (int idx = i - 5; idx < i + 5; ++idx)
                if (idx >= 0 && idx <= illust_list.Count)
                    Load(illust_list[idx]);
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
        //在浏览器中打开当前图片
        public void OpenInBrowser(object sender, EventArgs args)
        {
            if(illust_list.Count>0) 
                System.Diagnostics.Process.Start(String.Format("https://www.pixiv.net/artworks/{0}",illust_list[index].id));
        }
        //响应键盘
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
