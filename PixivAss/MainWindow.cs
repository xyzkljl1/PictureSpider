using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Runtime.InteropServices;
namespace PixivAss
{
    public partial class MainWindow : Form
    {
        public MainWindow()
        {
            pixivClient = new Client();
            InitializeComponent();
        }
        private Client pixivClient;
        private Button GoButton;
        private RichTextBox PageTextBox;
        private void onButtonClicked(object sender, EventArgs e)
        {
            PageTextBox.Text = pixivClient.Test(); 
        }
        private void InitializeComponent()
        {
            this.GoButton = new System.Windows.Forms.Button();
            this.PageTextBox = new System.Windows.Forms.RichTextBox();
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
            // PageTextBox
            // 
            this.PageTextBox.Location = new System.Drawing.Point(51, 93);
            this.PageTextBox.Name = "PageTextBox";
            this.PageTextBox.Size = new System.Drawing.Size(473, 329);
            this.PageTextBox.TabIndex = 1;
            this.PageTextBox.Text = "";
            // 
            // MainWindow
            // 
            this.ClientSize = new System.Drawing.Size(1801, 731);
            this.Controls.Add(this.PageTextBox);
            this.Controls.Add(this.GoButton);
            this.Name = "MainWindow";
            this.ResumeLayout(false);
        }
    }
}
