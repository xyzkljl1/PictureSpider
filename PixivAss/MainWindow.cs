using System;
using PixivAss;
using System.Windows.Forms;
using System.Threading.Tasks;
namespace PixivAss
{
    public partial class MainWindow : Form
    {
        struct ComboBoxItem {
            public int id;
            public string name;
            public Client.ExploreQueueType type;
            public ComboBoxItem(Tuple<Client.ExploreQueueType,int,string> data)
            {
                this.type = data.Item1;
                this.id = data.Item2;
                this.name = data.Item3;
            }
            public override string ToString()
            {
                if (type == Client.ExploreQueueType.Fav)
                    return "Fav";
                else if (type == Client.ExploreQueueType.FavR)
                    return "FavR";
                else if (type == Client.ExploreQueueType.Main)
                    return "Main";
                else if (type == Client.ExploreQueueType.MainR)
                    return "MainR";
                else
                    return name;
            }
        };

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
            queueComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            //设置事件
            KeyUp += new KeyEventHandler(MainExplorer.OnKeyUp);
            //PlayButton.Click += new EventHandler(onButtonClicked);
            NextButton.Click += new EventHandler(MainExplorer.SlideRight);
            PrevButton.Click += new EventHandler(MainExplorer.SlideLeft);
            idLabel.LinkClicked += new LinkLabelLinkClickedEventHandler(MainExplorer.OpenInBrowser);
            OpenLocalLabel.LinkClicked += new LinkLabelLinkClickedEventHandler(MainExplorer.OpenInLocal);
            SwitchBookmarkButton.Click += new EventHandler(MainExplorer.SwitchBookmarkStatus);
            queueComboBox.SelectedIndexChanged+=new EventHandler(onQueueComboBoxChanged);
            //queueComboBox.key
            //绑定属性
            PageLabel.DataBindings.Add(new Binding("Text", MainExplorer, "IndexText"));
            DescBrowser.DataBindings.Add(new Binding("DocumentText", MainExplorer, "DescText"));
            PlayButton.DataBindings.Add(new Binding("Text", MainExplorer, "TotalPageText"));
            idLabel.DataBindings.Add(new Binding("Text", MainExplorer, "IdText"));
            SwitchBookmarkButton.DataBindings.Add(new Binding("Image", MainExplorer, "FavIcon"));
            TagBox.DataBindings.Add(new Binding("Tags", MainExplorer, "Tags"));
            AuthorBox.DataBindings.Add(new Binding("UserId", MainExplorer, "UserId"));
            DataBindings.Add(new Binding("Text", pixivClient, "VerifyState"));

            //onListCheckBoxClick(null,null);
            UpdateQueueComboBox();
            //Debug
            pixivClient.Test();
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Tab)
                return true;
            if (keyData == Keys.Up || keyData == Keys.Down || keyData == Keys.Left || keyData == Keys.Right)
                return true;
            return false;
        }
        private void onButtonClicked(object sender, EventArgs e)
        {
            MainExplorer.Play();
            //pixivClient.Test();
        }
        private async void onQueueComboBoxChanged(object sender, EventArgs e)
        {
            using (BlockSyncContext.Enter())
            {
                var item = (ComboBoxItem)queueComboBox.SelectedItem;
                await MainExplorer.SetList(await pixivClient.GetExploreQueue(item.type,item.id));
            }
        }
        private async void UpdateQueueComboBox()
        {
            queueComboBox.Items.Clear();
            using (BlockSyncContext.Enter())
                foreach (var item in await pixivClient.GetExploreQueueName())
                    queueComboBox.Items.Add(new ComboBoxItem(item));
        }
        private void InitializeComponent()
        {
            System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainWindow));
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
            this.MainExplorer = new PixivAss.Explorer();
            this.queueComboBox = new System.Windows.Forms.ComboBox();
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
            flowLayoutPanel1.Controls.Add(this.AuthorBox);
            flowLayoutPanel1.Controls.Add(this.TagBox);
            flowLayoutPanel1.Controls.Add(this.DescBrowser);
            flowLayoutPanel1.Location = new System.Drawing.Point(12, 148);
            flowLayoutPanel1.Margin = new System.Windows.Forms.Padding(0);
            flowLayoutPanel1.Name = "flowLayoutPanel1";
            flowLayoutPanel1.Size = new System.Drawing.Size(164, 446);
            flowLayoutPanel1.TabIndex = 14;
            // 
            // AuthorBox
            // 
            this.AuthorBox.Location = new System.Drawing.Point(3, 3);
            this.AuthorBox.Name = "AuthorBox";
            this.AuthorBox.Size = new System.Drawing.Size(154, 20);
            this.AuthorBox.TabIndex = 12;
            this.AuthorBox.UserId = 0;
            // 
            // TagBox
            // 
            this.TagBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.TagBox.CausesValidation = false;
            this.TagBox.Location = new System.Drawing.Point(3, 29);
            this.TagBox.Name = "TagBox";
            this.TagBox.Size = new System.Drawing.Size(161, 68);
            this.TagBox.TabIndex = 9;
            this.TagBox.Tags = ((System.Collections.Generic.List<string>)(resources.GetObject("TagBox.Tags")));
            // 
            // DescBrowser
            // 
            this.DescBrowser.AllowWebBrowserDrop = false;
            this.DescBrowser.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.DescBrowser.CausesValidation = false;
            this.DescBrowser.IsWebBrowserContextMenuEnabled = false;
            this.DescBrowser.Location = new System.Drawing.Point(0, 100);
            this.DescBrowser.Margin = new System.Windows.Forms.Padding(0);
            this.DescBrowser.MinimumSize = new System.Drawing.Size(20, 20);
            this.DescBrowser.Name = "DescBrowser";
            this.DescBrowser.ScrollBarsEnabled = false;
            this.DescBrowser.Size = new System.Drawing.Size(164, 326);
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
            this.idLabel.Size = new System.Drawing.Size(74, 23);
            this.idLabel.TabIndex = 10;
            this.idLabel.TabStop = true;
            this.idLabel.Text = "pid0000000";
            this.idLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // OpenLocalLabel
            // 
            this.OpenLocalLabel.Location = new System.Drawing.Point(90, 12);
            this.OpenLocalLabel.Name = "OpenLocalLabel";
            this.OpenLocalLabel.Size = new System.Drawing.Size(83, 20);
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
            // queueComboBox
            // 
            this.queueComboBox.FormattingEnabled = true;
            this.queueComboBox.Location = new System.Drawing.Point(12, 125);
            this.queueComboBox.Name = "queueComboBox";
            this.queueComboBox.Size = new System.Drawing.Size(164, 20);
            this.queueComboBox.TabIndex = 19;
            this.queueComboBox.TabStop = false;
            // 
            // MainWindow
            // 
            this.ClientSize = new System.Drawing.Size(1044, 606);
            this.Controls.Add(this.queueComboBox);
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
        private ComboBox queueComboBox;
        private AuthorBox AuthorBox;
    }
}
