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

namespace PictureSpider.Pixiv
{
    partial class AuthorBox : UserControl
    {
        public event EventHandler AuthorModified;
        public int UserId
        {
            get { return user_id; }
            set
            {
                user_id = value;
                UpdateByUserId();
            }
        }
        private int user_id=-1;
        private Server client;
        private Boolean frozen = false;
        private Dictionary<CheckState, String> CheckState2Text = new Dictionary<CheckState, String> {
            { CheckState.Checked, "已关注" }, {  CheckState.Indeterminate, "已入列"  }, { CheckState.Unchecked, "未关注" } };

        public AuthorBox()
        {
            InitializeComponent();
            followCheckBox.CheckStateChanged += onCheckedChange;
        }

        public void SetClient(Server _client)
        {
            client = _client;
        }
        void UpdateByUserId()
        {
            frozen = true;
            if (user_id>=0&&client!=null)
            {
                var user = client.database.GetUserById(UserId).ConfigureAwait(false).GetAwaiter().GetResult();
                nameLabel.Text = user.userName;
                if (user.followed)
                    followCheckBox.CheckState = CheckState.Checked;
                else if(user.queued)
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
        void onCheckedChange(object sender, EventArgs e)
        {
            if (frozen)
                return;
            followCheckBox.Text = CheckState2Text[followCheckBox.CheckState];
            var user = new User(user_id, nameLabel.Text, followCheckBox.CheckState == CheckState.Checked, followCheckBox.CheckState == CheckState.Indeterminate);
            client.database.UpdateUser(user).Wait();
            AuthorModified(this,new EventArgs());
        }
    }
}
