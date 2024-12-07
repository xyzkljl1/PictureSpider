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
        private Boolean frozen = false;
        private static readonly Dictionary<CheckState, String> CheckState2Text = new Dictionary<CheckState, String> {
            { CheckState.Checked, "已关注" }, {  CheckState.Indeterminate, "已入列"  }, { CheckState.Unchecked, "未关注" } };

        public AuthorBox()
        {
            InitializeComponent();
            followCheckBox.CheckStateChanged += OnCheckedChange;
        }

        public void SetClient(BaseServer _server)
        {
            server = _server;
        }

        private void UpdateByUserId(string userId)
        {
            frozen = true;
            if (!string.IsNullOrEmpty(userId)&&server!=null)
            {
                user = server.GetUserById(userId);
                if(user!=null)
                {
                    nameLabel.Text = user.displayText;
                    if (user.followed)
                        followCheckBox.CheckState = CheckState.Checked;
                    else if (user.queued)
                        followCheckBox.CheckState = CheckState.Indeterminate;
                    else
                        followCheckBox.CheckState = CheckState.Unchecked;
                    followCheckBox.Text = CheckState2Text[followCheckBox.CheckState];
                }
            }
            else
            {
                nameLabel.Text = "";
                followCheckBox.CheckState = CheckState.Unchecked;
                followCheckBox.Text = "";
            }
            frozen = false;
        }

        private void OnCheckedChange(object sender, EventArgs e)
        {
            if (frozen)
                return;
            followCheckBox.Text = CheckState2Text[followCheckBox.CheckState];
            if(user!=null)
            {
                user.followed = followCheckBox.CheckState == CheckState.Checked;
                user.queued = followCheckBox.CheckState == CheckState.Indeterminate;
            }
            server.SetUserFollowOrQueue(user);
            AuthorModified?.Invoke(this, EventArgs.Empty);
        }
    }
}
