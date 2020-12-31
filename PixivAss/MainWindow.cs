using System;
using PixivAss;
using PixivAss.Data;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Threading.Tasks;
namespace PixivAss
{
    public partial class MainWindow : Form
    {
        private Client pixivClient;
        private Button PlayButton;
        //生成64位程序会导致无法用设计器编辑
        public MainWindow()
        {
            InitializeComponent();
            //初始化
            pixivClient = new Client();
            MainExplorer.SetClient(pixivClient);
            TagBox.SetClient(pixivClient);
            AuthorBox.SetClient(pixivClient);
            queueComboBox.SetClient(pixivClient);
            //UI
            //parent为主窗口的话，透明时会透出主窗口的背景，而主窗口背景不能设为透明，所以需要更改parent
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
            queueComboBox.QueueChanged+=new EventHandler<QueueChangeEventArgs>(onQueueComboBoxChanged);
            AuthorBox.AuthorModified += (object sender, EventArgs e) => queueComboBox.UpdateContent();
            PlayButton.Click += (object sender, EventArgs e) => MainExplorer.Play();
            RandomSlideCheckBox.Click += (object sender, EventArgs e) => MainExplorer.random_slide=RandomSlideCheckBox.Checked;
            //绑定属性
            PageLabel.DataBindings.Add(new Binding("Text", MainExplorer.GetBindHandle<string>("IndexText"), "Content"));
            DescBrowser.DataBindings.Add(new Binding("DocumentText", MainExplorer.GetBindHandle<string>("DescText"), "Content"));
            PlayButton.DataBindings.Add(new Binding("Text", MainExplorer.GetBindHandle<string>("TotalPageText"), "Content"));
            idLabel.DataBindings.Add(new Binding("Text", MainExplorer.GetBindHandle<string>("IdText"), "Content"));
            SwitchBookmarkButton.DataBindings.Add(new Binding("Image", MainExplorer.GetBindHandle<System.Drawing.Bitmap>("FavIcon"), "Content"));
            BookmarkPageLabel.DataBindings.Add(new Binding("Visible", MainExplorer.GetBindHandle<bool>("PageInvalid"), "Content"));
            TagBox.DataBindings.Add(new Binding("Tags", MainExplorer.GetBindHandle<List<string>>("Tags"), "Content"));
            AuthorBox.DataBindings.Add(new Binding("UserId", MainExplorer.GetBindHandle<int>("UserId"), "Content"));
            DataBindings.Add(new Binding("Text", pixivClient.GetBindHandle<string>("VerifyState"), "Content"));

            //onListCheckBoxClick(null,null);
            //Debug
            //pixivClient.Test();
            this.Hide();
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Tab)
                return true;
            if (keyData == Keys.Up || keyData == Keys.Down || keyData == Keys.Left || keyData == Keys.Right)
                return true;
            return false;
        }
        private void onQueueComboBoxChanged(object sender, QueueChangeEventArgs e)
        {
            using (BlockSyncContext.Enter())
               MainExplorer.SetList(pixivClient.GetExploreQueue(e.type,e.name).Result);
        }
        private void InitializeComponent()
        {
            System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
            PixivAss.BindHandleProvider bindHandleProvider2 = new PixivAss.BindHandleProvider();
            this.DescBrowser = new System.Windows.Forms.WebBrowser();
            this.PlayButton = new System.Windows.Forms.Button();
            this.PageLabel = new System.Windows.Forms.Label();
            this.NextButton = new System.Windows.Forms.Label();
            this.PrevButton = new System.Windows.Forms.Label();
            this.idLabel = new System.Windows.Forms.LinkLabel();
            this.OpenLocalLabel = new System.Windows.Forms.LinkLabel();
            this.SwitchBookmarkButton = new System.Windows.Forms.Label();
            this.BookmarkPageLabel = new System.Windows.Forms.Label();
            this.queueComboBox = new PixivAss.QueueComboBox();
            this.AuthorBox = new PixivAss.AuthorBox();
            this.TagBox = new PixivAss.TagBox();
            this.MainExplorer = new PixivAss.Explorer();
            this.RandomSlideCheckBox = new System.Windows.Forms.CheckBox();
            flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            flowLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.MainExplorer)).BeginInit();
            this.SuspendLayout();
            // 
            // flowLayoutPanel1
            // 
            flowLayoutPanel1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            flowLayoutPanel1.BackColor = System.Drawing.Color.Transparent;
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
            // DescBrowser
            // 
            this.DescBrowser.AllowWebBrowserDrop = false;
            this.DescBrowser.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.DescBrowser.CausesValidation = false;
            this.DescBrowser.IsWebBrowserContextMenuEnabled = false;
            this.DescBrowser.Location = new System.Drawing.Point(0, 184);
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
            // queueComboBox
            // 
            this.queueComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.queueComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.queueComboBox.FormattingEnabled = true;
            this.queueComboBox.Location = new System.Drawing.Point(3, 3);
            this.queueComboBox.Name = "queueComboBox";
            this.queueComboBox.Size = new System.Drawing.Size(101, 20);
            this.queueComboBox.TabIndex = 19;
            // 
            // AuthorBox
            // 
            this.AuthorBox.Location = new System.Drawing.Point(3, 29);
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
            this.TagBox.Location = new System.Drawing.Point(0, 52);
            this.TagBox.Margin = new System.Windows.Forms.Padding(0);
            this.TagBox.Name = "TagBox";
            this.TagBox.Size = new System.Drawing.Size(161, 132);
            this.TagBox.TabIndex = 9;
            this.TagBox.Tags = null;
            this.TagBox.WrapContents = false;
            // 
            // MainExplorer
            // 
            this.MainExplorer.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.MainExplorer.Location = new System.Drawing.Point(182, 12);
            this.MainExplorer.Name = "MainExplorer";
            this.MainExplorer.provider = bindHandleProvider2;
            this.MainExplorer.Size = new System.Drawing.Size(850, 582);
            this.MainExplorer.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.MainExplorer.TabIndex = 2;
            this.MainExplorer.TabStop = false;
            // 
            // RandomSlideCheckBox
            // 
            this.RandomSlideCheckBox.Location = new System.Drawing.Point(107, 0);
            this.RandomSlideCheckBox.Margin = new System.Windows.Forms.Padding(0);
            this.RandomSlideCheckBox.Name = "RandomSlideCheckBox";
            this.RandomSlideCheckBox.Size = new System.Drawing.Size(50, 24);
            this.RandomSlideCheckBox.TabIndex = 20;
            this.RandomSlideCheckBox.Text = "Rock";
            this.RandomSlideCheckBox.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.RandomSlideCheckBox.UseVisualStyleBackColor = true;
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
            flowLayoutPanel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.MainExplorer)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

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
    }
}
