using PixivAss;
using PixivAss.Data;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace PixivAss
{
    public partial class MainWindow : Form
    {
        private Client pixiv_client;
        //生成64位程序会导致无法用设计器编辑
        public MainWindow()
        {
            InitializeComponent();
            //初始化
            var config = LoadConfig();
            InitButton.Visible = config.ShowInitButton;
            pixiv_client = new Client(config);
            MainExplorer.SetClient(pixiv_client);
            TagBox.SetClient(pixiv_client);
            AuthorBox.SetClient(pixiv_client);
            queueComboBox.SetClient(pixiv_client);
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
            BookmarkPageLabel.Location = new System.Drawing.Point(0,0);
            //设置事件
            KeyUp += new KeyEventHandler(MainExplorer.OnKeyUp);
            NextButton.Click += new EventHandler(MainExplorer.SlideRight);
            PrevButton.Click += new EventHandler(MainExplorer.SlideLeft);
            idLabel.LinkClicked += new LinkLabelLinkClickedEventHandler(MainExplorer.OpenInBrowser);
            OpenLocalLabel.LinkClicked += new LinkLabelLinkClickedEventHandler(MainExplorer.OpenInLocal);
            SwitchBookmarkButton.MouseClick += new MouseEventHandler(MainExplorer.SwitchBookmarkStatus);
            queueComboBox.QueueChanged+=new EventHandler<QueueChangeEventArgs>(OnQueueComboBoxChanged);
            AuthorBox.AuthorModified += (object sender, EventArgs e) => queueComboBox.UpdateContent();
            PlayButton.Click += (object sender, EventArgs e) => MainExplorer.Play();
            RandomSlideCheckBox.Click += (object sender, EventArgs e) => MainExplorer.random_slide=RandomSlideCheckBox.Checked;
            InitButton.Click += (object sender, EventArgs e) =>pixiv_client.InitTask().Wait();
            SystemTrayIcon.DoubleClick += (object sender, EventArgs e) => this.Visible = !this.Visible;
            ExitAction.Click+= (object sender, EventArgs e) => this.Close(); 
            //绑定属性
            PageLabel.DataBindings.Add(new Binding("Text", MainExplorer.GetBindHandle<string>("IndexText"), "Content"));
            DescBrowser.DataBindings.Add(new Binding("DocumentText", MainExplorer.GetBindHandle<string>("DescText"), "Content"));
            PlayButton.DataBindings.Add(new Binding("Text", MainExplorer.GetBindHandle<string>("TotalPageText"), "Content"));
            idLabel.DataBindings.Add(new Binding("Text", MainExplorer.GetBindHandle<string>("IdText"), "Content"));
            SwitchBookmarkButton.DataBindings.Add(new Binding("Image", MainExplorer.GetBindHandle<System.Drawing.Bitmap>("FavIcon"), "Content"));
            BookmarkPageLabel.DataBindings.Add(new Binding("Visible", MainExplorer.GetBindHandle<bool>("PageInvalid"), "Content"));
            TagBox.DataBindings.Add(new Binding("Tags", MainExplorer.GetBindHandle<List<string>>("Tags"), "Content"));
            AuthorBox.DataBindings.Add(new Binding("UserId", MainExplorer.GetBindHandle<int>("UserId"), "Content"));
            DataBindings.Add(new Binding("Text", pixiv_client.GetBindHandle<string>("VerifyState"), "Content"));

            //onListCheckBoxClick(null,null);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            this.Hide();
            e.Cancel = true;
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Tab)
                return true;
            if (keyData == Keys.Up || keyData == Keys.Down || keyData == Keys.Left || keyData == Keys.Right)
                return true;
            return false;
        }
        private void OnQueueComboBoxChanged(object sender, QueueChangeEventArgs e)
        {
            using (BlockSyncContext.Enter())
               MainExplorer.SetList(pixiv_client.GetExploreQueue(e.type,e.name).Result);
        }
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
            PixivAss.BindHandleProvider bindHandleProvider1 = new PixivAss.BindHandleProvider();
            this.InitButton = new System.Windows.Forms.Button();
            this.queueComboBox = new PixivAss.QueueComboBox();
            this.RandomSlideCheckBox = new System.Windows.Forms.CheckBox();
            this.AuthorBox = new PixivAss.AuthorBox();
            this.TagBox = new PixivAss.TagBox();
            this.DescBrowser = new System.Windows.Forms.WebBrowser();
            this.PlayButton = new System.Windows.Forms.Button();
            this.PageLabel = new System.Windows.Forms.Label();
            this.NextButton = new System.Windows.Forms.Label();
            this.PrevButton = new System.Windows.Forms.Label();
            this.idLabel = new System.Windows.Forms.LinkLabel();
            this.OpenLocalLabel = new System.Windows.Forms.LinkLabel();
            this.SwitchBookmarkButton = new System.Windows.Forms.Label();
            this.BookmarkPageLabel = new System.Windows.Forms.Label();
            this.MainExplorer = new PixivAss.Explorer();
            this.SystemTrayIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.SystemTrayMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.ExitAction = new System.Windows.Forms.ToolStripMenuItem();
            flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            flowLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.MainExplorer)).BeginInit();
            this.SystemTrayMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // flowLayoutPanel1
            // 
            flowLayoutPanel1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            flowLayoutPanel1.BackColor = System.Drawing.Color.Transparent;
            flowLayoutPanel1.Controls.Add(this.InitButton);
            flowLayoutPanel1.Controls.Add(this.queueComboBox);
            flowLayoutPanel1.Controls.Add(this.RandomSlideCheckBox);
            flowLayoutPanel1.Controls.Add(this.AuthorBox);
            flowLayoutPanel1.Controls.Add(this.TagBox);
            flowLayoutPanel1.Controls.Add(this.DescBrowser);
            flowLayoutPanel1.Location = new System.Drawing.Point(12, 125);
            flowLayoutPanel1.Margin = new System.Windows.Forms.Padding(0);
            flowLayoutPanel1.Name = "flowLayoutPanel1";
            flowLayoutPanel1.Size = new System.Drawing.Size(164, 469);
            flowLayoutPanel1.TabIndex = 14;
            // 
            // InitButton
            // 
            this.InitButton.Location = new System.Drawing.Point(3, 3);
            this.InitButton.Name = "InitButton";
            this.InitButton.Size = new System.Drawing.Size(158, 74);
            this.InitButton.TabIndex = 20;
            this.InitButton.Text = "RunInitTask";
            this.InitButton.UseVisualStyleBackColor = true;
            this.InitButton.Visible = false;
            // 
            // queueComboBox
            // 
            this.queueComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.queueComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.queueComboBox.FormattingEnabled = true;
            this.queueComboBox.Location = new System.Drawing.Point(3, 83);
            this.queueComboBox.Name = "queueComboBox";
            this.queueComboBox.Size = new System.Drawing.Size(101, 20);
            this.queueComboBox.TabIndex = 19;
            // 
            // RandomSlideCheckBox
            // 
            this.RandomSlideCheckBox.Location = new System.Drawing.Point(107, 80);
            this.RandomSlideCheckBox.Margin = new System.Windows.Forms.Padding(0);
            this.RandomSlideCheckBox.Name = "RandomSlideCheckBox";
            this.RandomSlideCheckBox.Size = new System.Drawing.Size(50, 24);
            this.RandomSlideCheckBox.TabIndex = 20;
            this.RandomSlideCheckBox.Text = "Rock";
            this.RandomSlideCheckBox.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.RandomSlideCheckBox.UseVisualStyleBackColor = true;
            // 
            // AuthorBox
            // 
            this.AuthorBox.Location = new System.Drawing.Point(3, 109);
            this.AuthorBox.Name = "AuthorBox";
            this.AuthorBox.Size = new System.Drawing.Size(154, 20);
            this.AuthorBox.TabIndex = 12;
            this.AuthorBox.UserId = 0;
            // 
            // TagBox
            // 
            this.TagBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.TagBox.AutoScroll = true;
            this.TagBox.BackColor = System.Drawing.Color.Transparent;
            this.TagBox.CausesValidation = false;
            this.TagBox.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.TagBox.Location = new System.Drawing.Point(0, 132);
            this.TagBox.Margin = new System.Windows.Forms.Padding(0);
            this.TagBox.Name = "TagBox";
            this.TagBox.Size = new System.Drawing.Size(161, 132);
            this.TagBox.TabIndex = 9;
            this.TagBox.Tags = null;
            this.TagBox.WrapContents = false;
            // 
            // DescBrowser
            // 
            this.DescBrowser.AllowWebBrowserDrop = false;
            this.DescBrowser.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.DescBrowser.CausesValidation = false;
            this.DescBrowser.IsWebBrowserContextMenuEnabled = false;
            this.DescBrowser.Location = new System.Drawing.Point(0, 264);
            this.DescBrowser.Margin = new System.Windows.Forms.Padding(0);
            this.DescBrowser.MinimumSize = new System.Drawing.Size(20, 20);
            this.DescBrowser.Name = "DescBrowser";
            this.DescBrowser.ScrollBarsEnabled = false;
            this.DescBrowser.Size = new System.Drawing.Size(164, 291);
            this.DescBrowser.TabIndex = 7;
            this.DescBrowser.TabStop = false;
            // 
            // PlayButton
            // 
            this.PlayButton.CausesValidation = false;
            this.PlayButton.Font = new System.Drawing.Font("宋体", 24F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.PlayButton.Location = new System.Drawing.Point(12, 35);
            this.PlayButton.Name = "PlayButton";
            this.PlayButton.Size = new System.Drawing.Size(164, 87);
            this.PlayButton.TabIndex = 0;
            this.PlayButton.Text = "Go1/1";
            this.PlayButton.UseVisualStyleBackColor = true;
            // 
            // PageLabel
            // 
            this.PageLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.PageLabel.AutoSize = true;
            this.PageLabel.BackColor = System.Drawing.Color.Transparent;
            this.PageLabel.Font = new System.Drawing.Font("仿宋", 48F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.PageLabel.Location = new System.Drawing.Point(182, 530);
            this.PageLabel.Name = "PageLabel";
            this.PageLabel.Size = new System.Drawing.Size(124, 64);
            this.PageLabel.TabIndex = 6;
            this.PageLabel.Text = "0/0";
            // 
            // NextButton
            // 
            this.NextButton.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.NextButton.BackColor = System.Drawing.Color.Transparent;
            this.NextButton.Font = new System.Drawing.Font("宋体", 48F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
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
            this.PrevButton.Font = new System.Drawing.Font("宋体", 48F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.PrevButton.Location = new System.Drawing.Point(182, 9);
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
            this.SwitchBookmarkButton.Image = global::PixivAss.Properties.Resources.NotFav;
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
            this.BookmarkPageLabel.Font = new System.Drawing.Font("宋体", 40F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
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
            this.MainExplorer.Location = new System.Drawing.Point(182, 12);
            this.MainExplorer.Name = "MainExplorer";
            this.MainExplorer.provider = bindHandleProvider1;
            this.MainExplorer.Size = new System.Drawing.Size(850, 582);
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
            this.SystemTrayMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.ExitAction});
            this.SystemTrayMenu.Name = "SystemTrayMenu";
            this.SystemTrayMenu.Size = new System.Drawing.Size(181, 48);
            this.SystemTrayMenu.Opening += new System.ComponentModel.CancelEventHandler(this.SystemTrayMenu_Opening);
            // 
            // ExitAction
            // 
            this.ExitAction.Name = "ExitAction";
            this.ExitAction.Size = new System.Drawing.Size(180, 22);
            this.ExitAction.Text = "退出";
            // 
            // MainWindow
            // 
            this.ClientSize = new System.Drawing.Size(1044, 606);
            this.Controls.Add(this.BookmarkPageLabel);
            this.Controls.Add(this.OpenLocalLabel);
            this.Controls.Add(flowLayoutPanel1);
            this.Controls.Add(this.SwitchBookmarkButton);
            this.Controls.Add(this.idLabel);
            this.Controls.Add(this.PageLabel);
            this.Controls.Add(this.PrevButton);
            this.Controls.Add(this.NextButton);
            this.Controls.Add(this.MainExplorer);
            this.Controls.Add(this.PlayButton);
            this.KeyPreview = true;
            this.Name = "MainWindow";
            this.Load += new System.EventHandler(this.MainWindow_Load);
            flowLayoutPanel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.MainExplorer)).EndInit();
            this.SystemTrayMenu.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }
        private Config LoadConfig()
        {
            if(System.IO.File.Exists(@"config.json"))
            {
                using (JsonReader reader = new JsonTextReader(new System.IO.StreamReader("config.json")))
                {
                    JObject jsonObject = (JObject)JToken.ReadFrom(reader);
                    var config = new Config();
                    config.Proxy = jsonObject["Proxy"].ToString();
                    config.UserName = jsonObject["UserName"].ToString();
                    config.UserId = jsonObject["UserId"].ToString();
                    config.DownloadDir = jsonObject["DownloadDir"].ToString();
                    config.ConnectStr = jsonObject["ConnectStr"].ToString();
                    config.ShowInitButton = jsonObject.Value<Boolean>("ShowInitButton");
                    return config;
                }                
            }
            return new Config();
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
        private QueueComboBox queueComboBox;
        private Label BookmarkPageLabel;
        private CheckBox RandomSlideCheckBox;
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
