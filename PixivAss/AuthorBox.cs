using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using PixivAss.Data;

namespace PixivAss
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
        private Client client;
        private Boolean frozen = false;
        public AuthorBox()
        {
            InitializeComponent();
            followCheckBox.CheckedChanged += onCheckedChange;
        }

        public void SetClient(Client _client)
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
                {
                    followCheckBox.CheckState = CheckState.Checked;
                    followCheckBox.Text="已关注";
                }
                else if(user.queued)
                {
                    followCheckBox.CheckState = CheckState.Indeterminate;
                    followCheckBox.Text = "已入列";
                }
                else
                {
                    followCheckBox.CheckState = CheckState.Unchecked;
                    followCheckBox.Text = "未关注";
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
            var user = new User(user_id, nameLabel.Text, followCheckBox.CheckState == CheckState.Checked, followCheckBox.CheckState == CheckState.Indeterminate);
            client.database.UpdateUser(user).Wait();
            AuthorModified(this,new EventArgs());
        }
    }
}
