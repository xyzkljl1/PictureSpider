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
    partial class AuthorBox : UserControl
    {
        public event EventHandler AuthorModified;
        public string UserId
        {
            get { return user is null?"":user.displayId; }
            set
            {
                UpdateByUserId(value);
            }
        }
        private BaseUser user = null;
        private BaseServer server;
        private Boolean frozen = false;
        private Dictionary<CheckState, String> CheckState2Text = new Dictionary<CheckState, String> {
            { CheckState.Checked, "已关注" }, {  CheckState.Indeterminate, "已入列"  }, { CheckState.Unchecked, "未关注" } };

        public AuthorBox()
        {
            InitializeComponent();
            followCheckBox.CheckStateChanged += onCheckedChange;
        }

        public void SetClient(BaseServer _server)
        {
            server = _server;
        }
        void UpdateByUserId(string user_id)
        {
            frozen = true;
            if (!string.IsNullOrEmpty(user_id)&&server!=null)
            {
                user = server.GetUserById(user_id);
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
        void onCheckedChange(object sender, EventArgs e)
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
            AuthorModified(this,new EventArgs());
        }
    }
}
