﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using PixivAss.Data;
using PixivAss;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.ComponentModel;

namespace PixivAss
{
    class Explorer : PictureBox,IBindHandleProvider
    {  
        public BindHandleProvider provider { get; set; } = new BindHandleProvider();
        class ImageCache:IDisposable
        {
            public Illust illust;
            public List<Image> data;
            public DateTime required_time;
            public void Dispose()
            {
                data.ForEach(x => x.Dispose());
                data.Clear();
            }
        }
        private const int cache_size = 30;
        //ImageCache需要手动dispose
        private List<ImageCache> cache_pool = new List<ImageCache>();
        private List<Illust> illust_list = new List<Illust>();
        private int index = 0;
        private int sub_index = 0;
        private Image empty_image;
        public Client pixivClient;
        public bool random_slide = false;
        private Timer timer = new Timer();
        private bool playing = false;

        [Bindable(true)]
        public string IndexText{get {
                if (illust_list.Count <= 0)
                    return "";
                int count_1 = illust_list[index].validPageCount();
                int count_2 = illust_list[index].pageCount;
                return (sub_index+1).ToString()+"/"+ 
                       (count_1 == count_2 ? count_1.ToString():(count_1.ToString()+"("+count_2.ToString()+")"));
            } }
        [Bindable(true)]
        public string DescText{get { return illust_list.Count > 0 ? illust_list[index].title + "</br>" + illust_list[index].description : "";} }
        [Bindable(true)]
        public string TotalPageText{get { return (playing?"Pause":"Play")+"\n"+(illust_list.Count > 0 ? (index+1).ToString()+"/"+illust_list.Count.ToString() : ""); }}
        [Bindable(true)]
        public string IdText {get { return (illust_list.Count > 0 ? "["+illust_list[index].id+"]" : "None");}}
        [Bindable(true)]
        public List<string> Tags{get { return illust_list.Count > 0 ? illust_list[index].tags : new List<string>(); }}
        [Bindable(true)]
        public int UserId { get { return illust_list.Count > 0 ? illust_list[index].userId : -1; } }
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
        [Bindable(true)]
        public bool PageInvalid
        {
            get
            {
                if (illust_list.Count == 0)
                    return false;
                if (!illust_list[index].bookmarked)
                    return false;
                if (this.Image == null)
                    return false;
                int p = (int)this.Image.Tag;
                if(index<illust_list.Count&&p>=0&&p<illust_list[index].pageCount)
                    return !illust_list[index].isPageValid(p);
                return false;
            }
        }

        public Explorer()
        {
            this.SizeMode = PictureBoxSizeMode.Zoom;
            timer.Tick += new EventHandler(SlideRight);
            timer.Interval = 3000;
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
            empty_image.Tag = -1;
        }
        //播放
        public void Play()
        {
            if (playing)
                timer.Stop();
            else
                timer.Start();
            playing = !playing;
            this.NotifyChange<string>("TotalPageText");
        }
        //设置当前列表
        public void SetList(List<Illust> list)
        {
            this.Image = null;
            cache_pool.ForEach(y => y.Dispose());
            cache_pool.Clear();
            illust_list.Clear();            
            foreach (var illust in list)
            {
                bool pass = true;
                if(!illust.valid)
                {
                    pass = false;
                    for (int i = 0; i < illust.pageCount; i++)
                        if (File.Exists(String.Format("{0}/{1}", pixivClient.download_dir_main, Client.GetDownloadFileName(illust,i))))
                        {
                            pass = true;
                            break;
                        }
                }
                if (pass)
                    illust_list.Add(illust);
            }
            SlideTo(0,0,true);
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
                    if (illust.bookmarked && !illust.isPageValid(i))
                        continue;
                    string path = String.Format("{0}/{1}", pixivClient.download_dir_main, Client.GetDownloadFileName(illust, i));
                    if (File.Exists(path))
                    {
                        //如果图片过大就缩小一下防止占内存过大
                        var img = Image.FromFile(path);
                        if (img.Height * img.Width > 4096 * 4096)
                        {
                            int w = img.Width;
                            int h = img.Height;
                            if (w > h)
                            {
                                w = 4096;
                                h = (int)((float)w / img.Width * img.Height);
                            }
                            else
                            {
                                h = 4096;
                                w = (int)((float)h / img.Height * img.Width);
                            }
                            Bitmap new_img = new Bitmap(w, h);
                            Graphics g = Graphics.FromImage((System.Drawing.Image)new_img);
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            g.DrawImage(img, 0, 0, w, h);
                            g.Dispose();
                            img.Dispose();
                            img = new_img;
                        }
                        img.Tag = i;//图片在illust中的原本index
                        cache.data.Add(img);
                    }
                    else
                    {
                        var img = (Image)empty_image.Clone();
                        img.Tag = -1;
                        cache.data.Add(img);
                    }
                }
                cache_pool.Add(cache);
                while (cache_pool.Count > cache_size)
                {
                    cache_pool.Sort((l, r) => l.required_time.CompareTo(r.required_time));
                    int remove_size = cache_size / 2;
                    cache_pool.Take(remove_size).ToList<ImageCache>().ForEach(x=>x.Dispose());
                    cache_pool = cache_pool.Skip(remove_size).ToList<ImageCache>();
                }
                return cache;
            }
        }
        //切图实现
        private void SlideTo(int i, int j,bool force_update=false)
        {
            if (i >= illust_list.Count || i < 0)
                return;
            Illust illust = illust_list[i];
            ImageCache cache = Load(illust);
            if (j < cache.data.Count)
               this.Image = cache.data[j];
            else
               this.Image = empty_image;
            bool index_changed = index != i;
            bool sub_index_changed = sub_index!=j;
            index = i;//index和sub_index需要都更新完才能刷新
            sub_index = j;
            if (index_changed || force_update)
            {
                this.NotifyChangeRange<string>(new List<string> { "IdText", "DescText", "TotalPageText" });
                this.NotifyChange<int>("UserId");
                this.NotifyChange<List<string>>("Tags");
                this.NotifyChange<Bitmap>("FavIcon");
            }
            this.NotifyChange<bool>("PageInvalid");
            this.NotifyChange<string>("IndexText");
            for (int idx = i - 5; idx < i + 5; ++idx)
                if (idx >= 0 && idx < illust_list.Count)
                    Load(illust_list[idx]);
        }
        //标记当前为已读
        private void MarkReaded()
        {
            Illust illust = illust_list[index];
            if ((!illust.bookmarked) && !illust.readed)
            {
                illust.readed = true;
                pixivClient.database.UpdateIllustReaded(illust.id).Wait();
            }
        }
        private bool SlideRandom()
        {
            if (illust_list.Count < 1)
                return false;
            var random = new Random();
            int new_index = random.Next(0,illust_list.Count());
            int new_sub_index =random.Next(0,Load(illust_list[new_index]).data.Count);
            MarkReaded();
            SlideTo(new_index, new_sub_index);
            return true;
        }
        //切图的UI响应,通过UI切图时先将当前标记为已读
        private bool SlideVertical(int i,bool to_end=false)
        {
            if (random_slide)
                return SlideRandom();
            int new_index = index + i;
            if (new_index >= 0 && new_index < illust_list.Count)
            {
                MarkReaded();
                SlideTo(new_index, to_end? Load(illust_list[new_index]).data.Count - 1: 0);
                return true;
            }
            return false;
        }
        private bool SlideHorizon(int i)
        {
            if (random_slide)
                return SlideRandom();
            int new_sub_index = sub_index + i;
            if (new_sub_index >= 0 && new_sub_index < illust_list[index].pageCount
                &&new_sub_index < Load(illust_list[index]).data.Count)
                {
                    MarkReaded();
                    SlideTo(index, new_sub_index);
                    return true;
                }
            return false;
        }
        public void SlideRight(object sender,EventArgs args)
        {
            if (!SlideHorizon(1))
                SlideVertical(1);
        }
        public void SlideLeft(object sender, EventArgs args)
        {
            if (!SlideHorizon(-1))
                SlideVertical(-1, true);
        }
        /*
        public void SlideUp(object sender, EventArgs args)
        {
            SlideVertical(-1);
        }
        public void SlideDown(object sender, EventArgs args)
        {
            SlideVertical(1);
        }
        */ 
        //切换书签状态，无->bookmark->bookmarkPrivate
        public void SwitchBookmarkStatus(object sender, MouseEventArgs args)
        {
            if (index < 0 || index >= illust_list.Count)
                return;
            var illust = illust_list[index];
            if (args.Button == MouseButtons.Left)//左键切换整组是否收藏
            {
                if (!illust.bookmarked)//0->1
                    illust.bookmarked = true;
                else if (illust.bookmarkPrivate)//2->0
                    illust.bookmarked = illust.bookmarkPrivate = false;
                else//1->2
                    illust.bookmarkPrivate = true;
                pixivClient.database.UpdateIllustBookmarked(illust.id, illust.bookmarked, illust.bookmarkPrivate).Wait();
                this.NotifyChange<Bitmap>("FavIcon");
                this.NotifyChange<bool>("PageInvalid");
                this.NotifyChange<string>("IndexText");
            }
            else if (args.Button == MouseButtons.Right&&illust.bookmarked&&illust.validPageCount()>1)//右键切换单张收藏，不允许全部屏蔽
            {
                int i_index = (int)this.Image.Tag;
                illust.switchPageValid(i_index);
                pixivClient.database.UpdateIllustBookmarkEach(illust.id,illust.bookmarkEach).Wait();
                this.NotifyChange<Bitmap>("FavIcon");
                this.NotifyChange<bool>("PageInvalid");
                this.NotifyChange<string>("IndexText");
            }
        }
        //在浏览器中打开当前图片
        public void OpenInBrowser(object sender, EventArgs args)
        {
            if(illust_list.Count>0) 
                System.Diagnostics.Process.Start(String.Format("https://www.pixiv.net/artworks/{0}",illust_list[index].id));
        }
        //打开本地
        public void OpenInLocal(object sender, EventArgs args)
        {
            if (illust_list.Count > 0)
            {
                System.Diagnostics.Process process = new System.Diagnostics.Process();
                process.StartInfo.FileName =
                    String.Format("{0}/{1}", pixivClient.download_dir_main, Client.GetDownloadFileName(illust_list[index], sub_index));
                //使用系统默认浏览器，以最大化方式打开  
                process.StartInfo.Arguments = "rundl132.exe C://WINDOWS//system32//shimgvw.dll,ImageView_Fullscreen";
                process.StartInfo.UseShellExecute = true;
                process.Start();
                process.Close();
            }
        }
        //响应键盘
        public void OnKeyUp(object sender, KeyEventArgs e)
        {
            //左右键盘逐帧切换，到头时进入下一组
            //上下键直接进入下一组
            if (e.KeyCode == Keys.Left)
            {
                if(!SlideHorizon(-1))
                    SlideVertical(-1,true);
            }
            else if (e.KeyCode == Keys.Right)
            {
                if (!SlideHorizon(1))
                    SlideVertical(1);
            }
            else if (e.KeyCode == Keys.Up)
                SlideVertical(-1);
            else if (e.KeyCode == Keys.Down)
                SlideVertical(1);
        }
    }
}