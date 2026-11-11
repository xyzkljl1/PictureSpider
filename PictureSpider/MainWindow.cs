using PictureSpider;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace PictureSpider
{
    public delegate Task AsyncEventHandler(object sender, EventArgs e);
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public partial class MainWindow : Form, IMessageFilter
    {
        private List<BaseServer> servers = new List<BaseServer>();
        private ListenerServer listenerServer = null;
        private readonly Dictionary<Keys, Keys> pressedBrowserKeys = new Dictionary<Keys, Keys>();
        private readonly SemaphoreSlim browserKeyLock = new SemaphoreSlim(1, 1);
        private int queueLoadVersion;
        private bool queueCollapsed;
        //生成64位程序会导致无法用设计器编辑
        public MainWindow(Config config, Pixiv.Server _pixiv_server, List<BaseServer> other_servers)
        {
            //由于蜜汁原因，在x64下频繁绘制足够大的GIF时，其它控件不会根据databinding自动刷新，所以需要让BindHandle手动调用MainWindow.Update()
            //不确定是否会产生额外的刷新
            BindHandleProviderExtension.main_window = this;
            InitializeComponent();
            //初始化
            InitButton.Visible = config.ShowInitButton;
            InitializeLocalDebug(_pixiv_server);
            servers.Add(_pixiv_server);
            servers.AddRange(other_servers);
            //servers.Add(_twitter_server);
            {
                MainExplorer.SetSpecialDir(Path.Combine(config.PixivDownloadDir, "special"));
                TagBox.SetClient(servers[0]);
                AuthorBox.SetClient(servers[0]);
                queueTreeView.SetClient(servers);
            }
            //UI
            //parent为主窗口的话，透明时会透出主窗口的背景，而主窗口背景不能设为透明，所以需要更改parent
            SystemTrayIcon.Icon = Properties.Resources.baseIcon;
            PageLabel.Parent = this.MainExplorer;
            PageLabel.Location = new System.Drawing.Point(PageLabel.Location.X - MainExplorer.Location.X, PageLabel.Location.Y - MainExplorer.Location.Y);
            NextButton.Parent = this.MainExplorer;
            NextButton.Location = new System.Drawing.Point(NextButton.Location.X - MainExplorer.Location.X, NextButton.Location.Y - MainExplorer.Location.Y);
            PrevButton.Parent = this.MainExplorer;
            PrevButton.Location = new System.Drawing.Point(PrevButton.Location.X - MainExplorer.Location.X, PrevButton.Location.Y - MainExplorer.Location.Y);
            SwitchBookmarkButton.Parent = this.MainExplorer;
            SwitchBookmarkButton.Location = new System.Drawing.Point(SwitchBookmarkButton.Location.X - MainExplorer.Location.X, SwitchBookmarkButton.Location.Y - MainExplorer.Location.Y);
            BookmarkPageLabel.Parent = SwitchBookmarkButton;
            BookmarkPageLabel.Location = new System.Drawing.Point(0, 0);
            //设置事件
            MainExplorer.CurrentFileChanged += (s, e) =>
            {
                var file = MainExplorer.CurrentFile;
                AuthorBox.SetClient(MainExplorer.server, file);
                // 非联合队列沿用原来的 Pixiv 标签库。
                TagBox.SetClient(MainExplorer.server is AuthorHub.Server && file != null ? MainExplorer.server : servers[0], file);
            };
            FormClosing += OnClose;//关闭按钮不关闭，而是最小化
            NextButton.Click += new EventHandler(MainExplorer.SlideRight);
            PrevButton.Click += new EventHandler(MainExplorer.SlideLeft);
            idLabel.LinkClicked += new LinkLabelLinkClickedEventHandler(MainExplorer.OpenInBrowser);
            OpenLocalLabel.LinkClicked += new LinkLabelLinkClickedEventHandler(MainExplorer.OpenInLocal);
            SwitchBookmarkButton.MouseClick += new MouseEventHandler(MainExplorer.SwitchBookmarkStatus);
            queueTreeView.QueueChanged += async (s, e) => { await OnQueueChangedAsync(s, e); };
            queueTreeView.AfterSelect += UpdateSidePanelLayout;
            string queueTopNode = null;
            queueHeader.Click += (s, e) =>
            {
                if (!queueCollapsed)
                    queueTopNode = queueTreeView.TopNode?.Name;
                queueCollapsed = !queueCollapsed;
                sidePanel.SuspendLayout();
                queueTreeView.Visible = !queueCollapsed;
                UpdateSidePanelLayout(s, e);
                sidePanel.ResumeLayout();
                if (!queueCollapsed && queueTopNode != null)
                {
                    var nodes = queueTreeView.Nodes.Find(queueTopNode, true);
                    if (nodes.Length > 0)
                        queueTreeView.TopNode = nodes[0];
                }
            };
            AuthorBox.AuthorModified += (object sender, EventArgs e) => queueTreeView.UpdateContent();
            PlayButton.Click += (object sender, EventArgs e) => MainExplorer.Play();
            //InitButton.Click += (object sender, EventArgs e) =>_pixiv_server.InitTask().Wait();
            SystemTrayIcon.DoubleClick += (object sender, EventArgs e) => this.Visible = !this.Visible;
            ExitAction.Click += (object sender, EventArgs e) => { FormClosing -= OnClose; this.Close(); };//退出时先移除阻止关闭的handle
            //绑定属性
            PageLabel.DataBindings.Add(new Binding("Text", MainExplorer.GetBindHandle<string>("IndexText"), "Content"));
            DescBrowser.DataBindings.Add(new Binding("DocumentText", MainExplorer.GetBindHandle<string>("DescText"), "Content"));
            var playBinding = new Binding("Text", MainExplorer.GetBindHandle<string>("TotalPageText"), "Content", true);
            playBinding.Format += (s, e) => e.Value = e.Value?.ToString().Replace("\n", "  ");
            PlayButton.DataBindings.Add(playBinding);
            idLabel.DataBindings.Add(new Binding("Text", MainExplorer.GetBindHandle<string>("IdText"), "Content"));
            SwitchBookmarkButton.DataBindings.Add(new Binding("Image", MainExplorer.GetBindHandle<System.Drawing.Bitmap>("FavIcon"), "Content"));
            BookmarkPageLabel.DataBindings.Add(new Binding("Visible", MainExplorer.GetBindHandle<bool>("PageInvalid"), "Content"));
            TagBox.DataBindings.Add(new Binding("Tags", MainExplorer.GetBindHandle<List<string>>("Tags"), "Content"));
            AuthorBox.DataBindings.Add(new Binding("UserId", MainExplorer.GetBindHandle<string>("UserId"), "Content"));
            DataBindings.Add(new Binding("Text", _pixiv_server.GetBindHandle<string>("VerifyState"), "Content"));
            sidePanel.Layout += UpdateSidePanelLayout;
            UpdateSidePanelLayout(this, EventArgs.Empty);

            //onListCheckBoxClick(null,null);
            listenerServer = new ListenerServer(servers);
            Application.AddMessageFilter(this);
            Deactivate += (s, e) => pressedBrowserKeys.Clear();
            Disposed += (s, e) => Application.RemoveMessageFilter(this);
        }
        partial void InitializeLocalDebug(Pixiv.Server pixiv_server);
        private void UpdateSidePanelLayout(object sender, EventArgs e)
        {
            var selected = queueTreeView.SelectedNode;
            queueHeader.Text = (queueCollapsed ? "▸ 队列" : "▾ 队列")
                + (selected == null ? "" : $"：{selected.Parent?.Text} / {selected.Text}");
            int authorHeight = LogicalToDeviceUnits(32);
            int tagHeight = LogicalToDeviceUnits(100);
            //按两栏都显示时的原始列表高度计算，隐藏的空间全部留给描述栏。
            float queueHeight = Math.Max(0, sidePanel.ClientSize.Height - sidePanel.GetRowHeights()[0]
                - authorHeight - tagHeight - LogicalToDeviceUnits(200)) * 0.8f;
            queueHeight = queueHeader.Height + (queueCollapsed ? 0 : queueHeight);
            if (!AuthorBox.Visible)
                authorHeight = 0;
            if (!TagBox.Visible)
                tagHeight = 0;
            if (sidePanel.RowStyles[1].Height == queueHeight && sidePanel.RowStyles[2].Height == authorHeight
                && sidePanel.RowStyles[3].Height == tagHeight)
                return;
            sidePanel.SuspendLayout();
            sidePanel.RowStyles[1].Height = queueHeight;
            sidePanel.RowStyles[2].Height = authorHeight;
            sidePanel.RowStyles[3].Height = tagHeight;
            sidePanel.ResumeLayout();
        }
        private void OnClose(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }
        public bool PreFilterMessage(ref Message msg)
        {
            const int WM_KEYDOWN = 0x0100;
            const int WM_KEYUP = 0x0101;
            const int WM_SYSKEYDOWN = 0x0104;
            const int WM_SYSKEYUP = 0x0105;
            const int WM_UNICHAR = 0x0109;
            if (!Visible || Form.ActiveForm != this || msg.Msg < WM_KEYDOWN || msg.Msg > WM_UNICHAR)
                return false;

            // 在控件预处理前拦截全部键盘消息，包括队列树和嵌入网页的默认快捷键。
            var key = (Keys)msg.WParam.ToInt32() & Keys.KeyCode;
            if (msg.Msg == WM_KEYDOWN || msg.Msg == WM_SYSKEYDOWN)
            {
                var modifiers = Control.ModifierKeys;
                bool arrow = key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down;
                if ((arrow && (modifiers == Keys.None || modifiers == Keys.Control))
                    || (key == Keys.Delete && modifiers == Keys.None))
                    pressedBrowserKeys.TryAdd(key, key | modifiers);
            }
            else if (msg.Msg == WM_KEYUP || msg.Msg == WM_SYSKEYUP)
            {
                // 保留按下时的修饰键；长按重复的 KeyDown 只在松键时执行一次。
                if (pressedBrowserKeys.Remove(key, out var keyData))
                    OnBrowserKeyUp(keyData);
            }
            return true;
        }
        private async void OnBrowserKeyUp(Keys keyData)
        {
            await browserKeyLock.WaitAsync();
            try
            {
                if (!IsDisposed && Visible && Form.ActiveForm == this)
                    await MainExplorer.OnKeyUp(this, new KeyEventArgs(keyData));
            }
            finally
            {
                browserKeyLock.Release();
            }
        }
        private async Task OnQueueChangedAsync(object sender, QueueChangeEventArgs e)
        {
            int version = ++queueLoadVersion;
            var server = servers[e.ServerIndex];
            var files = await server.GetExplorerQueueItems(e.Item).ConfigureAwait(true);
            if (IsDisposed || version != queueLoadVersion)
                return;
            MainExplorer.SetList(server, files,
                e.Item.type == ExplorerQueue.QueueType.User ? e.Item.id : "");
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:验证平台兼容性", Justification = "<挂起>")]
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            PictureSpider.BindHandleProvider bindHandleProvider1 = new PictureSpider.BindHandleProvider();
            this.InitButton = new System.Windows.Forms.Button();
            this.queueTreeView = new PictureSpider.QueueTreeView();
            this.queueHeader = new System.Windows.Forms.Label();
            var queuePanel = new System.Windows.Forms.Panel();
            this.QueueTools = new System.Windows.Forms.FlowLayoutPanel();
            this.AuthorBox = new PictureSpider.AuthorBox();
            this.TagBox = new PictureSpider.TagBox();
            this.DescBrowser = new System.Windows.Forms.WebBrowser();
            this.PlayButton = new System.Windows.Forms.Button();
            this.PageLabel = new System.Windows.Forms.Label();
            this.NextButton = new System.Windows.Forms.Label();
            this.PrevButton = new System.Windows.Forms.Label();
            this.idLabel = new System.Windows.Forms.LinkLabel();
            this.OpenLocalLabel = new System.Windows.Forms.LinkLabel();
            this.SwitchBookmarkButton = new System.Windows.Forms.Label();
            this.BookmarkPageLabel = new System.Windows.Forms.Label();
            this.MainExplorer = new PictureSpider.Explorer();
            this.SystemTrayIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.SystemTrayMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.ExitAction = new System.Windows.Forms.ToolStripMenuItem();
            sidePanel = new System.Windows.Forms.TableLayoutPanel();
            sidePanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.MainExplorer)).BeginInit();
            this.SystemTrayMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // sidePanel
            // 
            sidePanel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)));
            sidePanel.BackColor = System.Drawing.Color.Transparent;
            sidePanel.ColumnCount = 1;
            sidePanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100));
            sidePanel.RowCount = 5;
            sidePanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            sidePanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 0));
            sidePanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32));
            sidePanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 100));
            sidePanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100));
            sidePanel.Controls.Add(this.QueueTools, 0, 0);
            sidePanel.Controls.Add(queuePanel, 0, 1);
            sidePanel.Controls.Add(this.AuthorBox, 0, 2);
            sidePanel.Controls.Add(this.TagBox, 0, 3);
            sidePanel.Controls.Add(this.DescBrowser, 0, 4);
            sidePanel.Location = new System.Drawing.Point(12, 73);
            sidePanel.Margin = new System.Windows.Forms.Padding(0);
            sidePanel.Name = "sidePanel";
            sidePanel.Size = new System.Drawing.Size(200, 533);
            sidePanel.TabIndex = 14;
            //
            // QueueTools
            //
            this.QueueTools.AutoSize = true;
            this.QueueTools.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.QueueTools.Dock = System.Windows.Forms.DockStyle.Fill;
            this.QueueTools.Margin = new System.Windows.Forms.Padding(0);
            this.QueueTools.Name = "QueueTools";
            this.QueueTools.Controls.Add(this.InitButton);
            // 
            // InitButton
            // 
            this.InitButton.Location = new System.Drawing.Point(3, 3);
            this.InitButton.Name = "InitButton";
            this.InitButton.Size = new System.Drawing.Size(120, 24);
            this.InitButton.TabIndex = 20;
            this.InitButton.Text = "RunInitTask";
            this.InitButton.UseVisualStyleBackColor = true;
            this.InitButton.Visible = false;
            //
            // queuePanel
            //
            queuePanel.Dock = System.Windows.Forms.DockStyle.Fill;
            queuePanel.Margin = new System.Windows.Forms.Padding(0);
            queuePanel.Name = "queuePanel";
            queuePanel.Controls.Add(this.queueTreeView);
            queuePanel.Controls.Add(this.queueHeader);
            this.queueHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.queueHeader.Height = 24;
            this.queueHeader.Name = "queueHeader";
            this.queueHeader.Text = "▾ 队列";
            this.queueHeader.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.queueHeader.AutoEllipsis = true;
            this.queueHeader.UseMnemonic = false;
            this.queueHeader.Cursor = System.Windows.Forms.Cursors.Hand;
            this.queueHeader.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            // 
            // queueTreeView
            // 
            this.queueTreeView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.queueTreeView.Margin = new System.Windows.Forms.Padding(0);
            this.queueTreeView.Name = "queueTreeView";
            this.queueTreeView.Size = new System.Drawing.Size(200, 260);
            this.queueTreeView.TabIndex = 19;
            // 
            // AuthorBox
            // 
            this.AuthorBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.AuthorBox.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.AuthorBox.Name = "AuthorBox";
            this.AuthorBox.Size = new System.Drawing.Size(192, 20);
            this.AuthorBox.TabIndex = 12;
            this.AuthorBox.UserId = "";
            this.AuthorBox.Visible = false;
            // 
            // TagBox
            // 
            this.TagBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.TagBox.AutoScroll = true;
            this.TagBox.BackColor = System.Drawing.Color.Transparent;
            this.TagBox.CausesValidation = false;
            this.TagBox.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.TagBox.Location = new System.Drawing.Point(0, 204);
            this.TagBox.Margin = new System.Windows.Forms.Padding(0);
            this.TagBox.Name = "TagBox";
            this.TagBox.Size = new System.Drawing.Size(200, 100);
            this.TagBox.TabIndex = 9;
            this.TagBox.Tags = null;
            this.TagBox.Visible = false;
            this.TagBox.WrapContents = false;
            // 
            // DescBrowser
            // 
            this.DescBrowser.AllowWebBrowserDrop = false;
            this.DescBrowser.Dock = System.Windows.Forms.DockStyle.Fill;
            this.DescBrowser.CausesValidation = false;
            this.DescBrowser.IsWebBrowserContextMenuEnabled = false;
            this.DescBrowser.Location = new System.Drawing.Point(0, 312);
            this.DescBrowser.Margin = new System.Windows.Forms.Padding(0);
            this.DescBrowser.MinimumSize = new System.Drawing.Size(20, 20);
            this.DescBrowser.Name = "DescBrowser";
            this.DescBrowser.ScrollBarsEnabled = true;
            this.DescBrowser.Size = new System.Drawing.Size(200, 200);
            this.DescBrowser.TabIndex = 7;
            this.DescBrowser.TabStop = false;
            // 
            // PlayButton
            // 
            this.PlayButton.CausesValidation = false;
            this.PlayButton.Font = new System.Drawing.Font("宋体", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.PlayButton.Location = new System.Drawing.Point(12, 35);
            this.PlayButton.Name = "PlayButton";
            this.PlayButton.Size = new System.Drawing.Size(200, 32);
            this.PlayButton.TabIndex = 0;
            this.PlayButton.Text = "Go1/1";
            this.PlayButton.UseVisualStyleBackColor = true;
            // 
            // PageLabel
            // 
            this.PageLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.PageLabel.AutoSize = true;
            this.PageLabel.BackColor = System.Drawing.Color.Transparent;
            this.PageLabel.Font = new System.Drawing.Font("仿宋", 48F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.PageLabel.Location = new System.Drawing.Point(218, 530);
            this.PageLabel.Name = "PageLabel";
            this.PageLabel.Size = new System.Drawing.Size(154, 80);
            this.PageLabel.TabIndex = 6;
            this.PageLabel.Text = "0/0";
            // 
            // NextButton
            // 
            this.NextButton.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.NextButton.BackColor = System.Drawing.Color.Transparent;
            this.NextButton.Font = new System.Drawing.Font("宋体", 48F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.NextButton.Location = new System.Drawing.Point(972, 12);
            this.NextButton.Name = "NextButton";
            this.NextButton.Size = new System.Drawing.Size(60, 508);
            this.NextButton.TabIndex = 8;
            this.NextButton.Text = ">";
            this.NextButton.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // PrevButton
            // 
            this.PrevButton.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.PrevButton.BackColor = System.Drawing.Color.Transparent;
            this.PrevButton.Font = new System.Drawing.Font("宋体", 48F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.PrevButton.Location = new System.Drawing.Point(218, 9);
            this.PrevButton.Name = "PrevButton";
            this.PrevButton.Size = new System.Drawing.Size(60, 511);
            this.PrevButton.TabIndex = 9;
            this.PrevButton.Text = "<";
            this.PrevButton.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // idLabel
            // 
            this.idLabel.Location = new System.Drawing.Point(10, 9);
            this.idLabel.Name = "idLabel";
            this.idLabel.Size = new System.Drawing.Size(83, 23);
            this.idLabel.TabIndex = 10;
            this.idLabel.TabStop = true;
            this.idLabel.Text = "[0000000]";
            this.idLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // OpenLocalLabel
            // 
            this.OpenLocalLabel.Location = new System.Drawing.Point(99, 12);
            this.OpenLocalLabel.Name = "OpenLocalLabel";
            this.OpenLocalLabel.Size = new System.Drawing.Size(74, 20);
            this.OpenLocalLabel.TabIndex = 18;
            this.OpenLocalLabel.TabStop = true;
            this.OpenLocalLabel.Text = "Open Local File";
            this.OpenLocalLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // SwitchBookmarkButton
            // 
            this.SwitchBookmarkButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.SwitchBookmarkButton.BackColor = System.Drawing.Color.Transparent;
            this.SwitchBookmarkButton.Cursor = System.Windows.Forms.Cursors.Hand;
            this.SwitchBookmarkButton.Image = global::PictureSpider.Properties.Resources.NotFav;
            this.SwitchBookmarkButton.Location = new System.Drawing.Point(941, 520);
            this.SwitchBookmarkButton.Name = "SwitchBookmarkButton";
            this.SwitchBookmarkButton.Size = new System.Drawing.Size(91, 74);
            this.SwitchBookmarkButton.TabIndex = 11;
            // 
            // BookmarkPageLabel
            // 
            this.BookmarkPageLabel.BackColor = System.Drawing.Color.Transparent;
            this.BookmarkPageLabel.CausesValidation = false;
            this.BookmarkPageLabel.Enabled = false;
            this.BookmarkPageLabel.Font = new System.Drawing.Font("宋体", 40F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.BookmarkPageLabel.Location = new System.Drawing.Point(941, 530);
            this.BookmarkPageLabel.Name = "BookmarkPageLabel";
            this.BookmarkPageLabel.Size = new System.Drawing.Size(91, 64);
            this.BookmarkPageLabel.TabIndex = 19;
            this.BookmarkPageLabel.Text = "X";
            this.BookmarkPageLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // MainExplorer
            // 
            this.MainExplorer.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.MainExplorer.Location = new System.Drawing.Point(218, 12);
            this.MainExplorer.Name = "MainExplorer";
            this.MainExplorer.provider = bindHandleProvider1;
            this.MainExplorer.Size = new System.Drawing.Size(814, 582);
            this.MainExplorer.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.MainExplorer.TabIndex = 2;
            this.MainExplorer.TabStop = false;
            // 
            // SystemTrayIcon
            // 
            this.SystemTrayIcon.ContextMenuStrip = this.SystemTrayMenu;
            this.SystemTrayIcon.Text = "PixivAss";
            this.SystemTrayIcon.Visible = true;
            // 
            // SystemTrayMenu
            // 
            this.SystemTrayMenu.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.SystemTrayMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.ExitAction});
            this.SystemTrayMenu.Name = "SystemTrayMenu";
            this.SystemTrayMenu.Size = new System.Drawing.Size(109, 28);
            this.SystemTrayMenu.Opening += new System.ComponentModel.CancelEventHandler(this.SystemTrayMenu_Opening);
            // 
            // ExitAction
            // 
            this.ExitAction.Name = "ExitAction";
            this.ExitAction.Size = new System.Drawing.Size(108, 24);
            this.ExitAction.Text = "退出";
            // 
            // MainWindow
            // 
            this.ClientSize = new System.Drawing.Size(1044, 606);
            this.Controls.Add(this.BookmarkPageLabel);
            this.Controls.Add(this.OpenLocalLabel);
            this.Controls.Add(sidePanel);
            this.Controls.Add(this.SwitchBookmarkButton);
            this.Controls.Add(this.idLabel);
            this.Controls.Add(this.PageLabel);
            this.Controls.Add(this.PrevButton);
            this.Controls.Add(this.NextButton);
            this.Controls.Add(this.MainExplorer);
            this.Controls.Add(this.PlayButton);
            this.Icon = global::PictureSpider.Properties.Resources.baseIcon;
            this.KeyPreview = true;
            this.Name = "MainWindow";
            this.Load += new System.EventHandler(this.MainWindow_Load);
            sidePanel.ResumeLayout(false);
            sidePanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.MainExplorer)).EndInit();
            this.SystemTrayMenu.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }
        private Button PlayButton;
        private Explorer MainExplorer;
        private Label PageLabel;
        private WebBrowser DescBrowser;
        private Label NextButton;
        private Label PrevButton;
        private LinkLabel idLabel;
        private Label SwitchBookmarkButton;
        private LinkLabel OpenLocalLabel;
        private TagBox TagBox;
        private AuthorBox AuthorBox;
        private QueueTreeView queueTreeView;
        private Label queueHeader;
        private TableLayoutPanel sidePanel;
        private FlowLayoutPanel QueueTools;
        private Label BookmarkPageLabel;
        private Button InitButton;
        private NotifyIcon SystemTrayIcon;
        private System.ComponentModel.IContainer components;

        private void MainWindow_Load(object sender, EventArgs e)
        {
        }

        private ContextMenuStrip SystemTrayMenu;

        private void SystemTrayMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {

        }

        private ToolStripMenuItem ExitAction;
    }
}
