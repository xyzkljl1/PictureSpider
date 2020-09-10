using System;
using PixivAss;
using System.Windows.Forms;
namespace PixivAss
{
    public partial class MainWindow : Form
    {
        private Client pixivClient;
        private Button GoButton;
        public MainWindow()
        {
            pixivClient = new Client();
            InitializeComponent();
            MainExplorer.SetClient(pixivClient);
            this.KeyUp+= new KeyEventHandler(MainExplorer.OnKeyUp);
            NextButton.Click+= new EventHandler(MainExplorer.SlideRight);
            PrevButton.Click += new EventHandler(MainExplorer.SlideLeft);
            MainExplorer.SetLabel(this.TopLabel,this.PageLabel);
            MainExplorer.SetAllIllust();
        }
        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData == Keys.Tab)
                return true;
            return false;
        }
        private void onButtonClicked(object sender, EventArgs e)
        {
            pixivClient.Test();
        }
        private void InitializeComponent()
        {
            this.GoButton = new System.Windows.Forms.Button();
            this.TopLabel = new System.Windows.Forms.Label();
            this.NextButton = new System.Windows.Forms.Button();
            this.PrevButton = new System.Windows.Forms.Button();
            this.PageLabel = new System.Windows.Forms.Label();
            this.MainExplorer = new PixivAss.Explorer();
            ((System.ComponentModel.ISupportInitialize)(this.MainExplorer)).BeginInit();
            this.SuspendLayout();
            // 
            // GoButton
            // 
            this.GoButton.Location = new System.Drawing.Point(51, 12);
            this.GoButton.Name = "GoButton";
            this.GoButton.Size = new System.Drawing.Size(131, 59);
            this.GoButton.TabIndex = 0;
            this.GoButton.Text = "Go";
            this.GoButton.UseVisualStyleBackColor = true;
            this.GoButton.Click += new System.EventHandler(this.onButtonClicked);
            // 
            // TopLabel
            // 
            this.TopLabel.AutoSize = true;
            this.TopLabel.Font = new System.Drawing.Font("宋体", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.TopLabel.Location = new System.Drawing.Point(222, 12);
            this.TopLabel.MaximumSize = new System.Drawing.Size(900, 100);
            this.TopLabel.Name = "TopLabel";
            this.TopLabel.Size = new System.Drawing.Size(29, 19);
            this.TopLabel.TabIndex = 0;
            this.TopLabel.Text = "23";
            // 
            // NextButton
            // 
            this.NextButton.Location = new System.Drawing.Point(1054, 92);
            this.NextButton.Name = "NextButton";
            this.NextButton.Size = new System.Drawing.Size(31, 727);
            this.NextButton.TabIndex = 4;
            this.NextButton.Text = "N";
            this.NextButton.UseVisualStyleBackColor = true;
            // 
            // PrevButton
            // 
            this.PrevButton.Location = new System.Drawing.Point(12, 92);
            this.PrevButton.Name = "PrevButton";
            this.PrevButton.Size = new System.Drawing.Size(33, 727);
            this.PrevButton.TabIndex = 5;
            this.PrevButton.Text = "P";
            this.PrevButton.UseVisualStyleBackColor = true;
            // 
            // PageLabel
            // 
            this.PageLabel.AutoSize = true;
            this.PageLabel.Font = new System.Drawing.Font("仿宋", 48F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.PageLabel.Location = new System.Drawing.Point(924, 755);
            this.PageLabel.Name = "PageLabel";
            this.PageLabel.Size = new System.Drawing.Size(124, 64);
            this.PageLabel.TabIndex = 6;
            this.PageLabel.Text = "0/0";
            // 
            // MainExplorer
            // 
            this.MainExplorer.Location = new System.Drawing.Point(51, 92);
            this.MainExplorer.Name = "MainExplorer";
            this.MainExplorer.Size = new System.Drawing.Size(997, 727);
            this.MainExplorer.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.MainExplorer.TabIndex = 2;
            this.MainExplorer.TabStop = false;
            this.MainExplorer.Click += new System.EventHandler(this.MainExplorer_Click);
            // 
            // MainWindow
            // 
            this.ClientSize = new System.Drawing.Size(1097, 831);
            this.Controls.Add(this.PageLabel);
            this.Controls.Add(this.PrevButton);
            this.Controls.Add(this.NextButton);
            this.Controls.Add(this.TopLabel);
            this.Controls.Add(this.MainExplorer);
            this.Controls.Add(this.GoButton);
            this.KeyPreview = true;
            this.Name = "MainWindow";
            ((System.ComponentModel.ISupportInitialize)(this.MainExplorer)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private Explorer MainExplorer;
        private Label TopLabel;

        private void MainExplorer_Click(object sender, EventArgs e)
        {

        }

        private Button NextButton;
        private Button PrevButton;
        private Label PageLabel;
    }
}
