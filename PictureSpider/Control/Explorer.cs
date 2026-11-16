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
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

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
        private readonly object cacheLock = new object();
        private ImageCache displayedCache;
        class AdjacentPreview
        {
            public int index;
            public int page;
            public int offset;
            public string path;
            public Rectangle bounds;
        }
        private List<AdjacentPreview> adjacentPreviews = new List<AdjacentPreview>();
        private bool previewJumping;
        private bool previewsEnabled = true;
        private const int previewSize = 120;
        private const int previewGap = 8;
        private const int previewBorder = 2;
        private (Rectangle bounds, bool horizontal, int count) previewLayout;
        private int previewIndex = -1;
        private int previewPage = -1;
        private List<ExplorerFileBase> file_list = new List<ExplorerFileBase>();
        private string queueUserId = "";
        private int index = 0;
        private int sub_index = 0;
        private Image empty_image;
        public BaseServer server;
        public event EventHandler CurrentFileChanged;
        public ExplorerFileBase CurrentFile => file_list.Count > 0 ? file_list[index] : null;
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
        public string UserId => file_list.Count > 0 ? file_list[index].userId : queueUserId;

        [Bindable(true)]
        public Bitmap FavIcon
        {
            get
            {
                if (file_list.Count == 0)
                    return Properties.Resources.NotFav;
                return file_list[index].GetBookmarkStatus(sub_index) switch
                {
                    BookmarkStatus.Public => Properties.Resources.Fav,
                    BookmarkStatus.Private => Properties.Resources.FavPrivate,
                    _ => Properties.Resources.NotFav
                };
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
        public void SetList(BaseServer _server,List<ExplorerFileBase> list,string userId = "", bool showPreviews = true)
        {
            server = _server;
            queueUserId = userId;
            previewsEnabled = showPreviews;
            previewIndex = previewPage = -1;
            lock (cacheLock)
            {
                adjacentPreviews = new List<AdjacentPreview>();
                displayedCache = null;
                this.Image = null;
                foreach (var cache in cache_pool)
                    cache.Value.Dispose();
                cache_pool.Clear();
            }
            file_list = list;
            if (file_list.Count>0)
            {
                int start_index = Math.Max(0, file_list.FindIndex(x => x.startFromHere));
                int start_page = 0;
                while (start_page < file_list[start_index].pageCount() - 1 && !file_list[start_index].isPageValid(start_page))
                    start_page++;
                SlideTo(start_index,start_page,true);
            }
            else
            {
                index = sub_index = 0;
                CurrentFileChanged?.Invoke(this, EventArgs.Empty);
                this.NotifyChangeRange<string>(new List<string> { "IdText", "DescText", "TotalPageText" });
                this.NotifyChange<string>("UserId");
                this.NotifyChange<List<string>>("Tags");
                this.NotifyChange<Bitmap>("FavIcon");
                this.NotifyChange<bool>("PageInvalid");
                this.NotifyChange<string>("IndexText");
            }
        }
        //载入某个Illust的一张图片
        private static Bitmap LoadWebp(string path)
        {
            IWICBitmapDecoder decoder = null;
            IWICBitmapFrameDecode frame = null;
            IWICFormatConverter converter = null;
            IWICImagingFactory factory = null;
            IntPtr sourcePtr = IntPtr.Zero;
            Bitmap bitmap = null;
            try
            {
                factory = (IWICImagingFactory)Activator.CreateInstance(Type.GetTypeFromCLSID(WicImagingFactoryClsid));
                factory.CreateDecoderFromFilename(path, IntPtr.Zero, GenericRead, WICDecodeOptions.WICDecodeMetadataCacheOnLoad, out decoder);
                decoder.GetFrame(0, out frame);
                factory.CreateFormatConverter(out converter);
                var pixelFormat = WicPixelFormat32bppBGRA;
                sourcePtr = Marshal.GetComInterfaceForObject(frame, typeof(IWICBitmapSource));
                converter.Initialize(sourcePtr, ref pixelFormat, WICBitmapDitherType.WICBitmapDitherTypeNone, IntPtr.Zero, 0, WICBitmapPaletteType.WICBitmapPaletteTypeCustom);

                converter.GetSize(out var width, out var height);
                bitmap = new Bitmap((int)width, (int)height, PixelFormat.Format32bppArgb);
                var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    ImageLockMode.WriteOnly, bitmap.PixelFormat);
                try
                {
                    converter.CopyPixels(IntPtr.Zero, (uint)data.Stride, (uint)(data.Stride * data.Height), data.Scan0);
                }
                finally
                {
                    bitmap.UnlockBits(data);
                }
                return bitmap;
            }
            catch
            {
                bitmap?.Dispose();
                throw;
            }
            finally
            {
                if (sourcePtr != IntPtr.Zero)
                    Marshal.Release(sourcePtr);
                if (converter != null)
                    Marshal.FinalReleaseComObject(converter);
                if (frame != null)
                    Marshal.FinalReleaseComObject(frame);
                if (decoder != null)
                    Marshal.FinalReleaseComObject(decoder);
                if (factory != null)
                    Marshal.FinalReleaseComObject(factory);
            }
        }

        private const uint GenericRead = 0x80000000;
        private static readonly Guid WicImagingFactoryClsid = new Guid("CACAF262-9370-4615-A13B-9F5539DA4C0A");
        private static readonly Guid WicPixelFormat32bppBGRA = new Guid("6fddc324-4e03-4bfe-b185-3d77768dc90f");

        private enum WICDecodeOptions
        {
            WICDecodeMetadataCacheOnDemand = 0,
            WICDecodeMetadataCacheOnLoad = 1
        }

        private enum WICBitmapDitherType
        {
            WICBitmapDitherTypeNone = 0
        }

        private enum WICBitmapPaletteType
        {
            WICBitmapPaletteTypeCustom = 0
        }

        [ComImport]
        [Guid("ec5ec8a9-c395-4314-9c77-54d7a935ff70")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IWICImagingFactory
        {
            void CreateDecoderFromFilename([MarshalAs(UnmanagedType.LPWStr)] string wzFilename, IntPtr pguidVendor,
                uint dwDesiredAccess, WICDecodeOptions metadataOptions, out IWICBitmapDecoder ppIDecoder);
            void CreateDecoderFromStream(IntPtr pIStream, IntPtr pguidVendor, WICDecodeOptions metadataOptions, IntPtr ppIDecoder);
            void CreateDecoderFromFileHandle(IntPtr hFile, IntPtr pguidVendor, WICDecodeOptions metadataOptions, IntPtr ppIDecoder);
            void CreateComponentInfo(ref Guid clsidComponent, IntPtr ppIInfo);
            void CreateDecoder(ref Guid guidContainerFormat, IntPtr pguidVendor, IntPtr ppIDecoder);
            void CreateEncoder(ref Guid guidContainerFormat, IntPtr pguidVendor, IntPtr ppIEncoder);
            void CreatePalette(IntPtr ppIPalette);
            void CreateFormatConverter(out IWICFormatConverter ppIFormatConverter);
        }

        [ComImport]
        [Guid("9edde9e7-8dee-47ea-99df-e6faf2ed44bf")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IWICBitmapDecoder
        {
            void QueryCapability(IntPtr pIStream, IntPtr pdwCapability);
            void Initialize(IntPtr pIStream, WICDecodeOptions cacheOptions);
            void GetContainerFormat(IntPtr pguidContainerFormat);
            void GetDecoderInfo(IntPtr ppIDecoderInfo);
            void CopyPalette(IntPtr pIPalette);
            void GetMetadataQueryReader(IntPtr ppIMetadataQueryReader);
            void GetPreview(IntPtr ppIBitmapSource);
            void GetColorContexts(uint cCount, IntPtr ppIColorContexts, IntPtr pcActualCount);
            void GetThumbnail(IntPtr ppIThumbnail);
            void GetFrameCount(out uint pCount);
            void GetFrame(uint index, out IWICBitmapFrameDecode ppIBitmapFrame);
        }

        [ComImport]
        [Guid("00000120-a8f2-4877-ba0a-fd2b6645fb94")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IWICBitmapSource
        {
            void GetSize(out uint puiWidth, out uint puiHeight);
            void GetPixelFormat(out Guid pPixelFormat);
            void GetResolution(out double pDpiX, out double pDpiY);
            void CopyPalette(IntPtr pIPalette);
            void CopyPixels(IntPtr prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer);
        }

        [ComImport]
        [Guid("3b16811b-6a43-4ec9-a813-3d930c13b940")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IWICBitmapFrameDecode
        {
            void GetSize(out uint puiWidth, out uint puiHeight);
            void GetPixelFormat(out Guid pPixelFormat);
            void GetResolution(out double pDpiX, out double pDpiY);
            void CopyPalette(IntPtr pIPalette);
            void CopyPixels(IntPtr prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer);
            void GetMetadataQueryReader(IntPtr ppIMetadataQueryReader);
            void GetColorContexts(uint cCount, IntPtr ppIColorContexts, IntPtr pcActualCount);
            void GetThumbnail(IntPtr ppIThumbnail);
        }

        [ComImport]
        [Guid("00000301-a8f2-4877-ba0a-fd2b6645fb94")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IWICFormatConverter
        {
            void GetSize(out uint puiWidth, out uint puiHeight);
            void GetPixelFormat(out Guid pPixelFormat);
            void GetResolution(out double pDpiX, out double pDpiY);
            void CopyPalette(IntPtr pIPalette);
            void CopyPixels(IntPtr prc, uint cbStride, uint cbBufferSize, IntPtr pbBuffer);
            void Initialize(IntPtr pISource, ref Guid dstFormat, WICBitmapDitherType dither,
                IntPtr pIPalette, double alphaThresholdPercent, WICBitmapPaletteType paletteTranslate);
            void CanConvert(ref Guid srcPixelFormat, ref Guid dstPixelFormat, out bool pfCanConvert);
        }

        private ImageCache Load(ExplorerFileBase eFile,int i)
        {
            var path = eFile.FilePath(i);
            lock (cacheLock)
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
                    if (File.Exists(path))
                    {
                        try
                        {
                            // 预览文件可能沿用原图扩展名，按文件头识别实际格式。
                            Span<byte> header = stackalloc byte[12];
                            int headerLength;
                            using (var stream = File.OpenRead(path))
                                headerLength = stream.ReadAtLeast(header, header.Length, false);
                            bool isWebp = headerLength == header.Length
                                && header.Slice(0, 4).SequenceEqual("RIFF"u8)
                                && header.Slice(8, 4).SequenceEqual("WEBP"u8);
                            Image img = isWebp
                                ? LoadWebp(path)
                                : Image.FromFile(path);
                            //我内存贼大，不用裁剪
                            img.Tag = i;//图片在illust中的原本index
                            cache.data = img;
                        }
                        catch (Exception e)
                        {
                            //视同文件损坏，删除
                            Console.WriteLine("Can't Load Image "+path);
                            Console.WriteLine(e.Message);
                            //暂定：不要删除，观察一段时间
                            //Console.WriteLine("Delete Image");
                            //File.Delete(path);
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
                lock (cacheLock)
                {
                    if (!cache_pool.TryAdd(path, cache))
                    {
                        cache.Dispose();
                        cache = cache_pool[path];
                        cache.required_time = DateTime.UtcNow;
                    }
                    while (cache_pool.Count > cache_size)
                    {
                        string oldest_cache = null;
                        DateTime oldest_cache_time = DateTime.MaxValue;
                        foreach (var tmp_cache in cache_pool)
                            if (!ReferenceEquals(tmp_cache.Value, displayedCache)
                                && !ReferenceEquals(tmp_cache.Value, cache)
                                && !adjacentPreviews.Any(x => x.path == tmp_cache.Key)
                                && tmp_cache.Value.required_time < oldest_cache_time)
                            {
                                oldest_cache = tmp_cache.Key;
                                oldest_cache_time = tmp_cache.Value.required_time;
                            }
                        if (oldest_cache is null)
                            break;
                        if (cache_pool.TryRemove(oldest_cache, out var ignored))
                            ignored.Dispose();
                    }
                    return cache;
                }
            }
        }
        private void Load(ExplorerFileBase eFile, int s,int t)
        {
            for(var i = s;i<t;++i)
                if(i>=0 && i<eFile.pageCount())
                    Load(eFile,i);
        }
        private (Rectangle bounds, bool horizontal, int count) GetPreviewLayout()
        {
            if (!previewsEnabled || file_list.Count == 0 || file_list[index].pageCount() <= 1 || Image == null
                || SizeMode != PictureBoxSizeMode.Zoom || Image.Tag is not int tag || tag < 0
                || ClientSize.Width <= Padding.Horizontal || ClientSize.Height <= Padding.Vertical)
                return default;

            // 与 PictureBox.Zoom 使用相同的主图矩形，预览只占现有留白。
            float scale = Math.Min((float)(ClientSize.Width - Padding.Horizontal) / Image.Width,
                (float)(ClientSize.Height - Padding.Vertical) / Image.Height);
            int width = (int)(Image.Width * scale);
            int height = (int)(Image.Height * scale);
            var main = new Rectangle((ClientSize.Width - width) / 2, (ClientSize.Height - height) / 2, width, height);
            int size = LogicalToDeviceUnits(previewSize);
            int gap = LogicalToDeviceUnits(previewGap);
            bool fillHeight = FindForm()?.WindowState == FormWindowState.Maximized;
            var regions = new[]
            {
                (bounds: Rectangle.FromLTRB(main.Right + gap, fillHeight ? 0 : gap,
                    ClientSize.Width - gap, ClientSize.Height - (fillHeight ? 0 : gap)), horizontal: false),
                (bounds: Rectangle.FromLTRB(gap, gap, ClientSize.Width - gap, main.Top - gap), horizontal: true)
            };
            var obstacles = Controls.Cast<System.Windows.Forms.Control>().Where(x => x.Visible)
                .Select(x => Rectangle.Inflate(x.Bounds, gap, gap)).ToList();
            // 主图比例变化时，只要原位置仍能容纳，就不移动或改变预览数量。
            if (!previewLayout.bounds.IsEmpty
                && regions.Any(x => x.horizontal == previewLayout.horizontal
                    && x.bounds.Contains(previewLayout.bounds))
                && (!fillHeight || previewLayout.horizontal
                    || previewLayout.bounds.Top == 0 && previewLayout.bounds.Bottom == ClientSize.Height)
                && obstacles.All(x => !x.IntersectsWith(previewLayout.bounds)))
                return previewLayout;

            foreach (var region in regions)
            {
                var area = region.bounds;
                if (area.Width < size || area.Height < size)
                    continue;
                var spaces = new List<Rectangle> { area };
                foreach (var obstacle in obstacles)
                {
                    var remaining = new List<Rectangle>();
                    foreach (var space in spaces)
                    {
                        var overlap = Rectangle.Intersect(space, obstacle);
                        if (overlap.Width <= 0 || overlap.Height <= 0)
                            remaining.Add(space);
                        else
                        {
                            remaining.Add(Rectangle.FromLTRB(space.Left, space.Top, overlap.Left, space.Bottom));
                            remaining.Add(Rectangle.FromLTRB(overlap.Right, space.Top, space.Right, space.Bottom));
                            remaining.Add(Rectangle.FromLTRB(space.Left, space.Top, space.Right, overlap.Top));
                            remaining.Add(Rectangle.FromLTRB(space.Left, overlap.Bottom, space.Right, space.Bottom));
                        }
                    }
                    spaces = remaining;
                }
                var best = Rectangle.Empty;
                int count = 0;
                foreach (var space in spaces)
                {
                    if (space.Width < size || space.Height < size)
                        continue;
                    if (fillHeight && !region.horizontal && (space.Top != 0 || space.Bottom != ClientSize.Height))
                        continue;
                    int slots = ((region.horizontal ? space.Width : space.Height) + gap) / (size + gap);
                    if (slots % 2 == 0)
                        slots--;
                    // 至少容纳前图、当前图、后图；始终使用奇数个位置。
                    if (slots < 3 || slots <= count)
                        continue;
                    count = slots;
                    int length = fillHeight && !region.horizontal ? space.Height : slots * (size + gap) - gap;
                    best = region.horizontal
                        ? new Rectangle(space.Left + (space.Width - length) / 2, space.Top, length, size)
                        : new Rectangle(space.Right - size, space.Top + (space.Height - length) / 2, size, length);
                }
                if (count > 0)
                    return (best, region.horizontal, count);
            }
            return default;
        }
        private async void UpdateAdjacentPreviews(bool force = false)
        {
            var layout = GetPreviewLayout();
            if (!force && layout == previewLayout && previewIndex == index && previewPage == sub_index)
                return;
            previewLayout = layout;
            previewIndex = index;
            previewPage = sub_index;
            var previews = new List<AdjacentPreview>();
            var files = file_list;
            if (layout.count > 0)
            {
                previews.Add(new AdjacentPreview { index = index, page = sub_index, path = files[index].FilePath(sub_index) });
                foreach (int direction in new[] { -1, 1 })
                {
                    int page = sub_index + direction;
                    int offset = direction;
                    while (page >= 0 && page < files[index].pageCount() && Math.Abs(offset) <= layout.count / 2)
                    {
                        if (files[index].isPageValid(page))
                        {
                            previews.Add(new AdjacentPreview { index = index, page = page, offset = offset, path = files[index].FilePath(page) });
                            offset += direction;
                        }
                        page += direction;
                    }
                }
            }
            lock (cacheLock)
                adjacentPreviews = previews;
            Cursor = Cursors.Default;
            Invalidate();
            try
            {
                // 只读取本地图片，复用主图缓存；预览本身不触发已读或下载。
                foreach (var preview in previews)
                {
                    await Task.Run(() => Load(files[preview.index], preview.page));
                    if (IsDisposed || Disposing || !ReferenceEquals(adjacentPreviews, previews))
                        return;
                    Invalidate(previewLayout.bounds);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or ExternalException)
            {
                Console.WriteLine($"Can't load adjacent preview: {ex.Message}");
            }
        }
        protected override void OnPaint(PaintEventArgs pe)
        {
            UpdateAdjacentPreviews();
            base.OnPaint(pe);
            lock (cacheLock)
            {
                foreach (var preview in adjacentPreviews)
                    preview.bounds = Rectangle.Empty;
                if (previewLayout.count == 0)
                    return;
                int size = LogicalToDeviceUnits(previewSize);
                int border = LogicalToDeviceUnits(previewBorder);
                using var pen = new Pen(SystemColors.Highlight, border);
                foreach (var preview in adjacentPreviews)
                {
                    if (preview.offset != 0 && !file_list[preview.index].isPageValid(preview.page)
                        || !cache_pool.TryGetValue(preview.path, out var cache)
                        || cache.data == null || cache.data.Tag is not int pageTag || pageTag < 0)
                        continue;
                    // 最大化的竖列均分剩余高度，首末单元贴边；缩略图本身不缩放。
                    int length = previewLayout.horizontal ? previewLayout.bounds.Width : previewLayout.bounds.Height;
                    int position = (int)((long)(previewLayout.count / 2 + preview.offset) * (length - size) / (previewLayout.count - 1));
                    preview.bounds = new Rectangle(previewLayout.bounds.X + (previewLayout.horizontal ? position : 0),
                        previewLayout.bounds.Y + (previewLayout.horizontal ? 0 : position), size, size);
                    var inner = Rectangle.Inflate(preview.bounds, -border, -border);
                    float scale = Math.Min((float)inner.Width / cache.data.Width, (float)inner.Height / cache.data.Height);
                    int width = (int)(cache.data.Width * scale);
                    int height = (int)(cache.data.Height * scale);
                    pe.Graphics.DrawImage(cache.data, new Rectangle(inner.X + (inner.Width - width) / 2,
                        inner.Y + (inner.Height - height) / 2, width, height));
                    if (preview.offset == 0)
                        pe.Graphics.DrawRectangle(pen, Rectangle.Inflate(preview.bounds, -border / 2, -border / 2));
                }
            }
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            previewLayout = default;
            previewIndex = -1;
            foreach (var preview in adjacentPreviews)
                preview.bounds = Rectangle.Empty;
            Invalidate();
        }
        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            previewLayout = default;
            previewIndex = -1;
            Invalidate();
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Cursor = adjacentPreviews.Any(x => x.offset != 0 && x.bounds.Contains(e.Location)) ? Cursors.Hand : Cursors.Default;
        }
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            Cursor = Cursors.Default;
        }
        protected override async void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left || previewJumping)
                return;
            var previews = adjacentPreviews;
            var target = previews.FirstOrDefault(x => x.bounds.Contains(e.Location));
            if (target == null || target.offset == 0 || !file_list[target.index].isPageValid(target.page))
                return;
            previewJumping = true;
            try
            {
                // 直接跳到目标，只处理离开的当前页，不把中间图组标成已读。
                await MarkReaded();
                if (!IsDisposed && !Disposing && ReferenceEquals(previews, adjacentPreviews))
                    SlideTo(target.index, target.page);
            }
            finally
            {
                previewJumping = false;
            }
        }
        //切图实现
        private void SlideTo(int i, int j,bool force_update=false)
        {
            if (i >= file_list.Count || i < 0)
                return;
            ExplorerFileBase illust = file_list[i];
            // 取出缓存到设置显示对象期间不能被后台淘汰；后台解码仍在锁外执行。
            lock (cacheLock)
            {
                ImageCache cache = Load(illust,j);
                displayedCache = cache;
                this.Image = cache.data;
            }
            bool index_changed = index != i;
            bool sub_index_changed = sub_index!=j;
            index = i;//index和sub_index需要都更新完才能刷新
            sub_index = j;
            if (index_changed || force_update)
            {
                CurrentFileChanged?.Invoke(this, EventArgs.Empty);
                this.NotifyChangeRange<string>(new List<string> { "IdText", "DescText", "TotalPageText" });
                this.NotifyChange<string>("UserId");
                this.NotifyChange<List<string>>("Tags");
            }
            this.NotifyChange<Bitmap>("FavIcon");
            this.NotifyChange<bool>("PageInvalid");
            this.NotifyChange<string>("IndexText");
            UpdateAdjacentPreviews(true);
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
        private Task MarkReaded()
        {
            return MarkReaded(this.index);
        }
        private async Task MarkReaded(int _index, bool wholeGroup=false)
        {
            var eFile = file_list[_index];
            await server.SwitchReaded(eFile, _index == index && !wholeGroup ? sub_index : -1);
            await Task.CompletedTask;
        }
        //切图的UI响应,通过UI切图时先将当前标记为已读
        //manually:是被SlideHorizon失败触发的还是被手动按上下键触发的
        private async Task<bool> SlideVertical(int offset,bool toEnd=false,bool manually=false)
        {
            if (index < 0 || index >= file_list.Count)
                return false;
            server.ExplorerQueueSwitchVertical(file_list,manually,offset,index,sub_index,toEnd,out var new_index,out var new_sub_index);
            if (new_index < 0 || new_sub_index < 0) return false;
            //将当前index和目标index之间的所有illust(不包括目标index)标记为已读
            //不考虑循环，如果index从3->5,就认为[3,5)已读,不考虑从3向前切换经过队头达到5的情况
            for(int i = Math.Min(new_index,index);i<= Math.Max(new_index, index);++i)
                if(i!=new_index)
                    await MarkReaded(i, manually && offset > 0);
            SlideTo(new_index, new_sub_index);
            return true;
        }
        private async Task<bool> SlideHorizon(int i)
        {
            if (index < 0 || index >= file_list.Count)
                return false;
            int step = i > 0 ? 1 : -1;
            for(int new_sub_index = sub_index + i;
                    new_sub_index>=0&&new_sub_index < file_list[index].pageCount();
                    new_sub_index+=step)
                if(file_list[index].isPageValid(new_sub_index))
                {
                    await MarkReaded();
                    SlideTo(index, new_sub_index);
                    return true;
                }
            return false;
        }
        public async void SlideRight(object sender,EventArgs args)
        {
            if (!await SlideHorizon(1))
                await SlideVertical(1);
        }
        public async void SlideLeft(object sender, EventArgs args)
        {
            if (!await SlideHorizon(-1))
                await SlideVertical(-1, true);
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
        //切换收藏状态，具体顺序由模块决定
        public async void SwitchBookmarkStatus(object sender, MouseEventArgs args)
        {
            if (index < 0 || index >= file_list.Count)
                return;
            var illust = file_list[index];
            if (args.Button == MouseButtons.Left)//左键切换收藏，具体范围由模块决定
                await SwitchBookmarkStatusImpLeftClick(illust);
            else if (args.Button == MouseButtons.Right&&illust.bookmarked&&illust.validPageCount()>1)//右键切换单张收藏，不允许全部屏蔽
                await SwitchBookmarkStatusImpRightClick(illust);
        }
        private async Task SwitchBookmarkStatusImpLeftClick(ExplorerFileBase illust)
        {
            await server.SwitchBookmark(illust, sub_index);
            this.NotifyChange<Bitmap>("FavIcon");
            this.NotifyChange<bool>("PageInvalid");
            this.NotifyChange<string>("IndexText");
            await Task.CompletedTask;
        }
        private async Task SwitchBookmarkStatusImpRightClick(ExplorerFileBase illust)
        {
            int i_index = (int)this.Image.Tag;
            await server.SwitchBookmarkEach(illust, i_index);
            this.NotifyChange<Bitmap>("FavIcon");
            this.NotifyChange<bool>("PageInvalid");
            this.NotifyChange<string>("IndexText");
            await Task.CompletedTask;
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
        public async Task OnKeyUp(object sender, KeyEventArgs e)
        {
            //左右键盘逐帧切换，到头时进入下一组
            //上下键直接进入下一组
            //按Del等于左键点击收藏按钮
            if (e.Control && (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right))
            {
                for (int i = 0; i < 5; ++i)
                    if (!await SlideHorizon(e.KeyCode == Keys.Left ? -1 : 1))
                        break;
            }
            else if (e.KeyCode == Keys.Left)
            {
                if(!await SlideHorizon(-1))
                    await SlideVertical(-1,true);
            }
            else if (e.KeyCode == Keys.Right)
            {
                if (!await SlideHorizon(1))
                    await SlideVertical(1);
            }
            else if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
            {
                for (int i = 0; i < (e.Control ? 5 : 1); ++i)
                    if (!await SlideVertical(e.KeyCode == Keys.Up ? -1 : 1,false,true))
                        break;
            }
            else if (e.KeyCode == Keys.Delete)
            {
                if (index < 0 || index >= file_list.Count)
                    return;
                var illust = file_list[index];
                await SwitchBookmarkStatusImpLeftClick(illust);
            }
        }
    }
}
