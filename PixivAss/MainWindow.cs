using System;
using PixivAss;
using System.Windows.Forms;
namespace PixivAss
{
    public partial class MainWindow : Form
    {
        private Client pixivClient;
        private Button PlayButton;
        public MainWindow()
        {
            InitializeComponent();
            //初始化
            pixivClient = new Client();
            MainExplorer.SetClient(pixivClient);
            //UI
            //parent为主窗口的话，透明时会透出主窗口的背景，而主窗口背景不能设为透明，所以需要更改parent
            PageLabel.Parent = this.MainExplorer;
            PageLabel.Location = new System.Drawing.Point(PageLabel.Location.X - MainExplorer.Location.X, PageLabel.Location.Y - MainExplorer.Location.Y);
            NextButton.Parent = this.MainExplorer;
            NextButton.Location = new System.Drawing.Point(NextButton.Location.X - MainExplorer.Location.X, NextButton.Location.Y - MainExplorer.Location.Y);
            PrevButton.Parent = this.MainExplorer;
            PrevButton.Location = new System.Drawing.Point(PrevButton.Location.X - MainExplorer.Location.X, PrevButton.Location.Y - MainExplorer.Location.Y);
            FavoriteButton.Parent = this.MainExplorer;
            FavoriteButton.Location = new System.Drawing.Point(FavoriteButton.Location.X - MainExplorer.Location.X, FavoriteButton.Location.Y - MainExplorer.Location.Y);
            //设置事件
            KeyUp+= new KeyEventHandler(MainExplorer.OnKeyUp);
            NextButton.Click+= new EventHandler(MainExplorer.SlideRight);
            PrevButton.Click += new EventHandler(MainExplorer.SlideLeft);
            idLabel.LinkClicked += new LinkLabelLinkClickedEventHandler(MainExplorer.OpenInBrowser);
            FavCheckBox.CheckedChanged += new EventHandler(this.onListCheckBoxClick);
            FavPrivateCheckBox.CheckedChanged += new EventHandler(this.onListCheckBoxClick);
            QueueCheckBox.CheckedChanged += new EventHandler(this.onListCheckBoxClick);
            //绑定属性
            PageLabel.DataBindings.Add(new Binding("Text", MainExplorer, "IndexText"));
            DescBrowser.DataBindings.Add(new Binding("DocumentText", MainExplorer, "DescText"));
            PlayButton.DataBindings.Add(new Binding("Text", MainExplorer, "TotalPageText"));
            idLabel.DataBindings.Add(new Binding("Text", MainExplorer, "IdText"));
            FavoriteButton.DataBindings.Add(new Binding("Image", MainExplorer, "FavIcon"));
            TagLabel.DataBindings.Add(new Binding("Text", MainExplorer, "TagText"));
            //Debug
            onListCheckBoxClick(null,null);
        }
        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData == Keys.Tab)
                return true;
            return false;
        }
        private void onButtonClicked(object sender, EventArgs e)
        {
            //MainExplorer.Play();
            pixivClient.Test().ConfigureAwait(false);
        }

        private void onListCheckBoxClick(object sender, EventArgs e)
        {
            MainExplorer.SetList(FavCheckBox.Checked,FavPrivateCheckBox.Checked,QueueCheckBox.Checked);
        }
        private void InitializeComponent()
        {
            System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
            this.TagLabel = new System.Windows.Forms.Label();
            this.DescBrowser = new System.Windows.Forms.WebBrowser();
            this.PlayButton = new System.Windows.Forms.Button();
            this.PageLabel = new System.Windows.Forms.Label();
            this.MainExplorer = new PixivAss.Explorer();
            this.NextButton = new System.Windows.Forms.Label();
            this.PrevButton = new System.Windows.Forms.Label();
            this.idLabel = new System.Windows.Forms.LinkLabel();
            this.FavoriteButton = new System.Windows.Forms.Label();
            this.FavCheckBox = new System.Windows.Forms.CheckBox();
            this.FavPrivateCheckBox = new System.Windows.Forms.CheckBox();
            this.QueueCheckBox = new System.Windows.Forms.CheckBox();
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
            flowLayoutPanel1.Controls.Add(this.TagLabel);
            flowLayoutPanel1.Controls.Add(this.DescBrowser);
            flowLayoutPanel1.Location = new System.Drawing.Point(12, 148);
            flowLayoutPanel1.Margin = new System.Windows.Forms.Padding(0);
            flowLayoutPanel1.Name = "flowLayoutPanel1";
            flowLayoutPanel1.Size = new System.Drawing.Size(164, 446);
            flowLayoutPanel1.TabIndex = 14;
            // 
            // TagLabel
            // 
            this.TagLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.TagLabel.AutoSize = true;
            this.TagLabel.Location = new System.Drawing.Point(3, 0);
            this.TagLabel.MaximumSize = new System.Drawing.Size(167, 0);
            this.TagLabel.Name = "TagLabel";
            this.TagLabel.Size = new System.Drawing.Size(119, 12);
            this.TagLabel.TabIndex = 12;
            this.TagLabel.Text = "#Tag #Tag #Tag #Tag";
            // 
            // DescBrowser
            // 
            this.DescBrowser.AllowWebBrowserDrop = false;
            this.DescBrowser.CausesValidation = false;
            this.DescBrowser.IsWebBrowserContextMenuEnabled = false;
            this.DescBrowser.Location = new System.Drawing.Point(0, 12);
            this.DescBrowser.Margin = new System.Windows.Forms.Padding(0);
            this.DescBrowser.MinimumSize = new System.Drawing.Size(20, 20);
            this.DescBrowser.Name = "DescBrowser";
            this.DescBrowser.ScrollBarsEnabled = false;
            this.DescBrowser.Size = new System.Drawing.Size(164, 454);
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
            this.PlayButton.Click += new System.EventHandler(this.onButtonClicked);
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
            // MainExplorer
            // 
            this.MainExplorer.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.MainExplorer.Location = new System.Drawing.Point(182, 12);
            this.MainExplorer.Name = "MainExplorer";
            this.MainExplorer.Size = new System.Drawing.Size(850, 582);
            this.MainExplorer.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.MainExplorer.TabIndex = 2;
            this.MainExplorer.TabStop = false;
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
            this.idLabel.Location = new System.Drawing.Point(13, 9);
            this.idLabel.Name = "idLabel";
            this.idLabel.Size = new System.Drawing.Size(163, 23);
            this.idLabel.TabIndex = 10;
            this.idLabel.TabStop = true;
            this.idLabel.Text = "pid0000000";
            this.idLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // FavoriteButton
            // 
            this.FavoriteButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.FavoriteButton.BackColor = System.Drawing.Color.Transparent;
            this.FavoriteButton.Cursor = System.Windows.Forms.Cursors.Hand;
            this.FavoriteButton.Image = global::PixivAss.Properties.Resources.NotFav;
            this.FavoriteButton.Location = new System.Drawing.Point(941, 520);
            this.FavoriteButton.Name = "FavoriteButton";
            this.FavoriteButton.Size = new System.Drawing.Size(91, 74);
            this.FavoriteButton.TabIndex = 11;
            // 
            // FavCheckBox
            // 
            this.FavCheckBox.AutoSize = true;
            this.FavCheckBox.Location = new System.Drawing.Point(12, 129);
            this.FavCheckBox.Name = "FavCheckBox";
            this.FavCheckBox.Size = new System.Drawing.Size(48, 16);
            this.FavCheckBox.TabIndex = 15;
            this.FavCheckBox.Text = "收藏";
            this.FavCheckBox.UseVisualStyleBackColor = true;
            // 
            // FavPrivateCheckBox
            // 
            this.FavPrivateCheckBox.AutoSize = true;
            this.FavPrivateCheckBox.Location = new System.Drawing.Point(66, 129);
            this.FavPrivateCheckBox.Name = "FavPrivateCheckBox";
            this.FavPrivateCheckBox.Size = new System.Drawing.Size(54, 16);
            this.FavPrivateCheckBox.TabIndex = 16;
            this.FavPrivateCheckBox.Text = "收藏R";
            this.FavPrivateCheckBox.UseVisualStyleBackColor = true;
            // 
            // QueueCheckBox
            // 
            this.QueueCheckBox.AutoSize = true;
            this.QueueCheckBox.Checked = true;
            this.QueueCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.QueueCheckBox.Location = new System.Drawing.Point(126, 129);
            this.QueueCheckBox.Name = "QueueCheckBox";
            this.QueueCheckBox.Size = new System.Drawing.Size(48, 16);
            this.QueueCheckBox.TabIndex = 17;
            this.QueueCheckBox.Text = "队列";
            this.QueueCheckBox.UseVisualStyleBackColor = true;
            // 
            // MainWindow
            // 
            this.ClientSize = new System.Drawing.Size(1044, 606);
            this.Controls.Add(this.QueueCheckBox);
            this.Controls.Add(this.FavPrivateCheckBox);
            this.Controls.Add(this.FavCheckBox);
            this.Controls.Add(flowLayoutPanel1);
            this.Controls.Add(this.FavoriteButton);
            this.Controls.Add(this.idLabel);
            this.Controls.Add(this.PageLabel);
            this.Controls.Add(this.PrevButton);
            this.Controls.Add(this.NextButton);
            this.Controls.Add(this.MainExplorer);
            this.Controls.Add(this.PlayButton);
            this.KeyPreview = true;
            this.Name = "MainWindow";
            flowLayoutPanel1.ResumeLayout(false);
            flowLayoutPanel1.PerformLayout();
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
        private Label FavoriteButton;
        private Label TagLabel;

        private CheckBox FavCheckBox;
        private CheckBox FavPrivateCheckBox;
        private CheckBox QueueCheckBox;
    }
}
