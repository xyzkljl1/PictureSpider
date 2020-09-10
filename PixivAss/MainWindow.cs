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
            GoButton.KeyUp+= new KeyEventHandler(MainExplorer.OnKeyUp);
        }
        private void onButtonClicked(object sender, EventArgs e)
        {
            pixivClient.Test();
        }
        private void InitializeComponent()
        {
            this.GoButton = new System.Windows.Forms.Button();
            this.MainExplorer = new PixivAss.Explorer();
            ((System.ComponentModel.ISupportInitialize)(this.MainExplorer)).BeginInit();
            this.SuspendLayout();
            // 
            // GoButton
            // 
            this.GoButton.Location = new System.Drawing.Point(51, 12);
            this.GoButton.Name = "GoButton";
            this.GoButton.Size = new System.Drawing.Size(165, 60);
            this.GoButton.TabIndex = 0;
            this.GoButton.Text = "Go";
            this.GoButton.UseVisualStyleBackColor = true;
            this.GoButton.Click += new System.EventHandler(this.onButtonClicked);
            // 
            // MainExplorer
            // 
            this.MainExplorer.Location = new System.Drawing.Point(51, 92);
            this.MainExplorer.Name = "MainExplorer";
            this.MainExplorer.Size = new System.Drawing.Size(1034, 727);
            this.MainExplorer.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.MainExplorer.TabIndex = 2;
            this.MainExplorer.TabStop = false;
            // 
            // MainWindow
            // 
            this.ClientSize = new System.Drawing.Size(1097, 831);
            this.Controls.Add(this.MainExplorer);
            this.Controls.Add(this.GoButton);
            this.Name = "MainWindow";
            ((System.ComponentModel.ISupportInitialize)(this.MainExplorer)).EndInit();
            this.ResumeLayout(false);
        }

        private Explorer MainExplorer;
    }
}
