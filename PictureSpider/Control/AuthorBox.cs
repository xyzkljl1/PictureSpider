using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using PictureSpider;

namespace PictureSpider
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    partial class AuthorBox : UserControl
    {
        public event EventHandler AuthorModified;
        public string UserId
        {
            get => user is null?"":user.displayId;
            set => UpdateByUserId(value);
        }
        private BaseUser user = null;
        private BaseServer server;
        private ExplorerFileBase currentFile;
        private Boolean frozen = false;
        private static readonly Dictionary<CheckState, String> CheckState2Text = new Dictionary<CheckState, String> {
            { CheckState.Checked, "已关注" }, {  CheckState.Indeterminate, "已入列"  }, { CheckState.Unchecked, "未关注" } };

        public AuthorBox()
        {
            InitializeComponent();
            followCheckBox.CheckStateChanged += OnCheckedChange;
        }

        public void SetClient(BaseServer _server, ExplorerFileBase file = null)
        {
            server = _server;
            currentFile = file;
        }

        private void UpdateByUserId(string userId)
        {
            frozen = true;
            user = !string.IsNullOrEmpty(userId)&&server!=null ? server.GetUserById(userId, currentFile) : null;
            followCheckBox.Enabled = user != null;
            if(user!=null)
            {
                nameLabel.Text = string.IsNullOrEmpty(user.ModuleAbbreviation)
                    ? user.displayText : $"[{user.ModuleAbbreviation}] {user.displayText}";
                if (user.followed)
                    followCheckBox.CheckState = CheckState.Checked;
                else if (user.queued)
                    followCheckBox.CheckState = CheckState.Indeterminate;
                else
                    followCheckBox.CheckState = CheckState.Unchecked;
                followCheckBox.Text = CheckState2Text[followCheckBox.CheckState];
            }
            else
            {
                nameLabel.Text = "";
                followCheckBox.CheckState = CheckState.Unchecked;
                followCheckBox.Text = "";
            }
            frozen = false;
        }

        private async void OnCheckedChange(object sender, EventArgs e)
        {
            if (frozen||user==null)
                return;
            followCheckBox.Text = CheckState2Text[followCheckBox.CheckState];
            if(user!=null)
            {
                user.followed = followCheckBox.CheckState == CheckState.Checked;
                user.queued = followCheckBox.CheckState == CheckState.Indeterminate;
            }
            await server.SetUserFollowOrQueue(user);
            AuthorModified?.Invoke(this, EventArgs.Empty);
            await Task.CompletedTask;
        }
    }
}
