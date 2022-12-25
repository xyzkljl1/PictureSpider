namespace PictureSpider
{
    partial class AuthorBox
    {
        /// <summary> 
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region 组件设计器生成的代码

        /// <summary> 
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.followCheckBox = new System.Windows.Forms.CheckBox();
            this.nameLabel = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // followCheckBox
            // 
            this.followCheckBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.followCheckBox.AutoSize = true;
            this.followCheckBox.Location = new System.Drawing.Point(88, 0);
            this.followCheckBox.Name = "followCheckBox";
            this.followCheckBox.Size = new System.Drawing.Size(15, 14);
            this.followCheckBox.TabIndex = 0;
            this.followCheckBox.ThreeState = true;
            this.followCheckBox.UseMnemonic = false;
            this.followCheckBox.UseVisualStyleBackColor = true;
            // 
            // nameLabel
            // 
            this.nameLabel.Location = new System.Drawing.Point(3, 0);
            this.nameLabel.Name = "nameLabel";
            this.nameLabel.Size = new System.Drawing.Size(79, 16);
            this.nameLabel.TabIndex = 1;
            this.nameLabel.Text = "label1";
            // 
            // AuthorBox
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.nameLabel);
            this.Controls.Add(this.followCheckBox);
            this.Name = "AuthorBox";
            this.Size = new System.Drawing.Size(154, 20);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox followCheckBox;
        private System.Windows.Forms.Label nameLabel;
    }
}
