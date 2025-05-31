using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using PictureSpider;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Collections.Concurrent;

namespace PictureSpider
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    class Explorer : PictureBox,IBindHandleProvider
    {  
        public BindHandleProvider provider { get; set; } = new BindHandleProvider();
        class ImageCache:IDisposable
        {
            public Image data=null;
            public DateTime required_time;
            public void Dispose()
            {
                if(data != null)
                {
                    data.Dispose();
                    data = null;
                }
            }
            ~ImageCache()
            {
                Dispose();
            }
        }
        //cache的大小，应大于SlideTo最多可能预加载的数量，以防止反复加载卸载和卸载正在显示的图片
        private const int cache_size = 500;
        //ImageCache需要手动dispose
        //key是图片路径
        private ConcurrentDictionary<string,ImageCache> cache_pool = new ConcurrentDictionary<string, ImageCache>();
        private List<ExplorerFileBase> file_list = new List<ExplorerFileBase>();
        private int index = 0;
        private int sub_index = 0;
        private Image empty_image;
        public BaseServer server;
        private Timer timer = new Timer();
        private bool playing = false;

        [Bindable(true)]
        public string IndexText{get {
                if (file_list.Count <= 0)
                    return "";
                int count1 = file_list[index].validPageCount();
                int count2 = file_list[index].pageCount();
                return (sub_index+1).ToString()+"/"+ 
                       (count1 == count2 ? count1.ToString():(count2.ToString()+"("+count1.ToString()+")"));
            } }
        [Bindable(true)]
        public string DescText => file_list.Count > 0 ?$"{file_list[index].title}</br>{file_list[index].debugMessage}</br>{file_list[index].description}" : "";

        [Bindable(true)]
        public string TotalPageText => (playing?"Pause":"Play")+"\n"+(file_list.Count > 0 ? (index+1).ToString()+"/"+file_list.Count.ToString() : "0/0");

        [Bindable(true)]
        public string IdText => (file_list.Count > 0 ? "["+file_list[index].id+"]" : "None");

        [Bindable(true)]
        public List<string> Tags => file_list.Count > 0 ? file_list[index].tags : new List<string>();

        [Bindable(true)]
        public string UserId => file_list.Count > 0 ? file_list[index].userId : "";

        [Bindable(true)]
        public Bitmap FavIcon
        {
            get
            {
                if (file_list.Count == 0)
                    return Properties.Resources.NotFav;
                if (file_list[index].bookmarked)
                    return file_list[index].bookmarkPrivate ? Properties.Resources.FavPrivate : Properties.Resources.Fav;
                return Properties.Resources.NotFav;
            }
        }
        [Bindable(true)]
        public bool PageInvalid
        {
            get
            {
                if (file_list.Count == 0)
                    return false;
                if (!file_list[index].bookmarked)
                    return false;
                if (this.Image == null)
                    return false;
                int p = (int)this.Image.Tag;
                if(index<file_list.Count&&p>=0&&p<file_list[index].pageCount())
                    return !file_list[index].isPageValid(p);
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
        public void SetSpecialDir(string special_dir)
        {
            string path = special_dir + "/" + "empty_pic.png";
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
        public void SetList(BaseServer _server,List<ExplorerFileBase> list)
        {
            server = _server;
            this.Image = null;
            foreach (var cache in cache_pool)
                cache.Value.Dispose();
            cache_pool.Clear();
            file_list = list;
            if (file_list.Count>0)
                SlideTo(0,0,true);
            else
            {
                index = sub_index = 0;
                this.NotifyChangeRange<string>(new List<string> { "IdText", "DescText", "TotalPageText" });
                this.NotifyChange<string>("UserId");
                this.NotifyChange<List<string>>("Tags");
                this.NotifyChange<Bitmap>("FavIcon");
                this.NotifyChange<bool>("PageInvalid");
                this.NotifyChange<string>("IndexText");
            }
        }
        //载入某个Illust的一张图片
        private ImageCache Load(ExplorerFileBase eFile,int i)
        {
            var path = eFile.FilePath(i);
            {//hit
                if (cache_pool.TryGetValue(path, out var cache))
                {
                    cache.required_time = DateTime.UtcNow;
                    return cache;
                }
            }
            {//not hit
                ImageCache cache = new ImageCache();
                cache.required_time = DateTime.UtcNow;
                //不检查i越界，由调用者保证
                if ((!eFile.bookmarked) || eFile.isPageValid(i))
                {
                    if (File.Exists(path)
                        && Path.GetExtension(path).ToLower() != ".webp")//自带Image读取webp会直接报out of memeory,此时可能是图片尚未转换，不删除原图
                    {
                        try
                        {
                            Image img = Image.FromFile(path);
                            //我内存贼大，不用裁剪
                            img.Tag = i;//图片在illust中的原本index
                            cache.data = img;
                        }
                        catch (Exception e)
                        {
                            //视同文件损坏，删除
                            Console.WriteLine("Can't Load Image "+path);
                            Console.WriteLine(e.Message);
                            Console.WriteLine("Delete Image");
                            File.Delete(path);
                            var img = (Image)empty_image.Clone();
                            img.Tag = -1;
                            cache.data = img;
                        }
                    }
                    else
                    {
                        var img = new Bitmap(1, 1);//(Image)empty_image.Clone();
                        img.Tag = -1;
                        cache.data = img;
                    }
                }
                if (!cache_pool.TryAdd(path, cache))//开头就检测过hit，如果此时已经存在，那肯定是刚加进去的，没必要更新required_time
                    cache.Dispose();
                while (cache_pool.Count > cache_size)
                {
                    //C#里可修改的Pair类是什么？
                    string oldest_cache = null;
                    DateTime oldest_cache_time = DateTime.MaxValue;
                    foreach (var tmp_cache in cache_pool)
                        if (tmp_cache.Value.required_time < oldest_cache_time)
                        {
                            oldest_cache = tmp_cache.Key;
                            oldest_cache_time = tmp_cache.Value.required_time;
                        }
                    if (oldest_cache is null)
                        continue;
                    if (cache_pool.TryRemove(oldest_cache, out var ignored))
                        ignored.Dispose();
                }
                return cache;
            }
        }
        private void Load(ExplorerFileBase eFile, int s,int t)
        {
            for(var i = s;i<t;++i)
                if(i>=0 && i<eFile.pageCount())
                    Load(eFile,i);
        }
        //切图实现
        private void SlideTo(int i, int j,bool force_update=false)
        {
            if (i >= file_list.Count || i < 0)
                return;
            ExplorerFileBase illust = file_list[i];
            ImageCache cache = Load(illust,j);
            this.Image = cache.data;
            bool index_changed = index != i;
            bool sub_index_changed = sub_index!=j;
            index = i;//index和sub_index需要都更新完才能刷新
            sub_index = j;
            if (index_changed || force_update)
            {
                this.NotifyChangeRange<string>(new List<string> { "IdText", "DescText", "TotalPageText" });
                this.NotifyChange<string>("UserId");
                this.NotifyChange<List<string>>("Tags");
                this.NotifyChange<Bitmap>("FavIcon");
            }
            this.NotifyChange<bool>("PageInvalid");
            this.NotifyChange<string>("IndexText");
            //载入相邻illustGroup的前n张图和当前illusgGroup浏览位置附近n张图
            for (int idx = i - 5; idx < i + 5; ++idx)
                if (idx >= 0 && idx < file_list.Count && idx!=i)
                {
                    //不能写成Task.Run(() => Load(illust_list[idx]));否则[]运行函数时才执行，此时idx的值已经改变
                    ExplorerFileBase tmp = file_list[idx];
                    Task.Run(() => Load(tmp,0,30));
                }
            Task.Run(() => Load(illust, j-20, j+20));
        }
        //标记当前为已读
        private void MarkReaded()
        {
            MarkReaded(this.index);
        }
        private void MarkReaded(int _index)
        {
            var eFile = file_list[_index];
            if ((!eFile.bookmarked) && !eFile.readed)
            {
                eFile.readed = true;
                server.SetReaded(eFile);
            }
        }
        //切图的UI响应,通过UI切图时先将当前标记为已读
        //manually:是被SlideHorizon失败触发的还是被手动按上下键触发的
        private bool SlideVertical(int offset,bool toEnd=false,bool manually=false)
        {
            if (index < 0 || index >= file_list.Count)
                return false;
            server.ExplorerQueueSwitchVertical(file_list,manually,offset,index,sub_index,toEnd,out var new_index,out var new_sub_index);
            if (new_index < 0 || new_sub_index < 0) return false;
            //将当前index和目标index之间的所有illust(不包括目标index)标记为已读
            //不考虑循环，如果index从3->5,就认为[3,5)已读,不考虑从3向前切换经过队头达到5的情况
            for(int i = Math.Min(new_index,index);i<= Math.Max(new_index, index);++i)
                if(i!=new_index)
                    MarkReaded(i);
            SlideTo(new_index, new_sub_index);
            return true;
        }
        private bool SlideHorizon(int i)
        {
            if (index < 0 || index >= file_list.Count)
                return false;
            int step = i > 0 ? 1 : -1;
            for(int new_sub_index = sub_index + i;
                    new_sub_index>=0&&new_sub_index < file_list[index].pageCount();
                    new_sub_index+=step)
                if(file_list[index].isPageValid(new_sub_index))
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
            if (index < 0 || index >= file_list.Count)
                return;
            var illust = file_list[index];
            if (args.Button == MouseButtons.Left)//左键切换整组是否收藏
                SwitchBookmarkStatusImpLeftClick(illust);
            else if (args.Button == MouseButtons.Right&&illust.bookmarked&&illust.validPageCount()>1)//右键切换单张收藏，不允许全部屏蔽
                SwitchBookmarkStatusImpRightClick(illust);
        }
        private void SwitchBookmarkStatusImpLeftClick(ExplorerFileBase illust)
        {
            if (server.tripleBookmarkState)
            {
                if (!illust.bookmarked)//0->1
                    illust.bookmarked = true;
                else if (illust.bookmarkPrivate)//2->0
                    illust.bookmarked = illust.bookmarkPrivate = false;
                else//1->2
                    illust.bookmarkPrivate = true;
            }
            else
                illust.bookmarked = !illust.bookmarked;
            server.SetBookmarked(illust);
            this.NotifyChange<Bitmap>("FavIcon");
            this.NotifyChange<bool>("PageInvalid");
            this.NotifyChange<string>("IndexText");
        }
        private void SwitchBookmarkStatusImpRightClick(ExplorerFileBase illust)
        {
            int i_index = (int)this.Image.Tag;
            illust.switchPageValid(i_index);
            server.SetBookmarkEach(illust);
            this.NotifyChange<Bitmap>("FavIcon");
            this.NotifyChange<bool>("PageInvalid");
            this.NotifyChange<string>("IndexText");
        }
        //在浏览器中打开当前图片
        public void OpenInBrowser(object sender, EventArgs args)
        {
            //更新net版本后UseShellExecute默认值变为false导致无法打开url，需要指定为true
            if (file_list.Count>0)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(file_list[index].WebsiteURL(sub_index)) { UseShellExecute = true });
        }
        //打开本地
        public void OpenInLocal(object sender, EventArgs args)
        {
            if (file_list.Count <= 0) return;
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            process.StartInfo.FileName = file_list[index].FilePath(sub_index);
            //使用系统默认浏览器，以最大化方式打开  
            process.StartInfo.Arguments = "rundl132.exe C://WINDOWS//system32//shimgvw.dll,ImageView_Fullscreen";
            process.StartInfo.UseShellExecute = true;
            process.Start();
            process.Close();
        }
        //响应键盘
        public void OnKeyUp(object sender, KeyEventArgs e)
        {
            //左右键盘逐帧切换，到头时进入下一组
            //上下键直接进入下一组
            //按Del等于左键点击收藏按钮
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
                SlideVertical(-1,false,true);
            else if (e.KeyCode == Keys.Down)
                SlideVertical(1,false,true);
            else if (e.KeyCode == Keys.Delete)
            {
                if (index < 0 || index >= file_list.Count)
                    return;
                var illust = file_list[index];
                SwitchBookmarkStatusImpLeftClick(illust);
            }
        }
    }
}
