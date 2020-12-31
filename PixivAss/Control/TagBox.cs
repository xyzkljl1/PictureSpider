using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using PixivAss.Data;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.ComponentModel;

namespace PixivAss
{
    class TagBox : System.Windows.Forms.FlowLayoutPanel
    {
        public List<string> Tags
        {
            get { return null; }
            set {SetTags(value); }
        }
        private Client client;
        private Dictionary<string, TagStatus> tags_status;
        private Dictionary<string, string>    tags_desc;
        private bool block_signal = false;
        private Dictionary<CheckState, TagStatus> CheckState2TagStatus =new Dictionary<CheckState, TagStatus>
                                                                    { { CheckState.Checked, TagStatus.Follow },
                                                                      { CheckState.Unchecked, TagStatus.None },
                                                                      { CheckState.Indeterminate, TagStatus.Ignore }};
        private Dictionary<TagStatus, CheckState> TagStatus2CheckState = new Dictionary<TagStatus, CheckState>
                                                                    { { TagStatus.Follow,CheckState.Checked },
                                                                      { TagStatus.None, CheckState.Unchecked },
                                                                      { TagStatus.Ignore, CheckState.Indeterminate }};
        public TagBox()
        {
            this.BackColor = System.Drawing.Color.Transparent;
            this.Margin = new System.Windows.Forms.Padding(0);
            this.FlowDirection = FlowDirection.TopDown;
            this.AutoScroll = true;
            this.WrapContents = false;
        }

        public void SetClient(Client _client)
        {
            client = _client;
            tags_status = Task.Run(client.database.GetAllTagsStatus).ConfigureAwait(false).GetAwaiter().GetResult();
            tags_desc = Task.Run(client.database.GetAllTagsDesc).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        private void SetTags(List<string> tags)
        {
            if (tags is null)
                return;
            block_signal = true;
            this.SuspendLayout();
            if (tags.Count> Controls.Count)//添加控件，多余的无需删除
            {
                while(tags.Count> Controls.Count)
                {
                    var item = new CheckBox();
                    item.CheckStateChanged += new EventHandler(ItemCheckHandler);
                    item.ThreeState = true;
                    item.Margin= new System.Windows.Forms.Padding(0);
                    item.Padding = new System.Windows.Forms.Padding(0);
                    item.AutoSize = true;
                    this.Controls.Add(item);
                }
            }
            foreach(CheckBox item in Controls)
            {
                item.Text = "";
                item.CheckState = CheckState.Unchecked;
            }
            var tag_group = new Dictionary<CheckState, List<string>>
                                        { { CheckState.Checked ,new List<string>()},
                                          { CheckState.Indeterminate, new List<string>() },
                                          { CheckState.Unchecked, new List<string>() } };
            foreach (var tag in tags)
                if (tags_status.Keys.Contains(tag))
                    tag_group[TagStatus2CheckState[tags_status[tag]]].Add(tag);
                else
                    tag_group[CheckState.Unchecked].Add(tag);
            //按未关注->已关注->忽略的顺序
            var iterator = Controls.GetEnumerator();
            foreach (var state in new List<CheckState>{ CheckState.Unchecked,CheckState.Checked, CheckState.Indeterminate })
                foreach (var tag in tag_group[state])
                {
                    iterator.MoveNext();
                    if (tags_desc.ContainsKey(tag) && !string.IsNullOrEmpty(tags_desc[tag]))
                        ((CheckBox)iterator.Current).Text = String.Format("{0}`{1}", tag, tags_desc[tag]);
                    else
                        ((CheckBox)iterator.Current).Text = tag;
                    ((CheckBox)iterator.Current).CheckState = state;
                    ((CheckBox)iterator.Current).Visible = true;
                }
            while (iterator.MoveNext())
                ((CheckBox)iterator.Current).Visible = false;            
            this.ResumeLayout();
            block_signal = false;
        }
        private void ItemCheckHandler(object sender, EventArgs e)
        {
            if (block_signal)
                return;
            var text = ((CheckBox)sender).Text;
            if (text.Count() == 0)
                return;
            if (text.Contains('`'))
                text = text.Substring(0,text.IndexOf('`'));
            var new_status = CheckState2TagStatus[((CheckBox)sender).CheckState];
            if (!tags_status.ContainsKey(text))
            {
                client.database.UpdateTagStatus(text, new_status).Wait();
                tags_status.Add(text, new_status);
            }
            else if(tags_status[text]!= new_status)
            {
                client.database.UpdateTagStatus(text, new_status).Wait();
                tags_status[text] = new_status;
            }
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            return true;
        }
    }
}
