using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using PictureSpider;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.ComponentModel;

namespace PictureSpider
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    class TagBox : FlowLayoutPanel
    {
        public List<string> Tags
        {
            get => null;
            set => SetTags(value);
        }
        private BaseServer server;
        private Dictionary<string, TagStatus> tagsStatus;
        private Dictionary<string, string>    tagsDesc;
        private bool blockSignal = false;
        private static readonly Dictionary<CheckState, TagStatus> CheckState2TagStatus =new Dictionary<CheckState, TagStatus>
                                                                    { { CheckState.Checked, TagStatus.Follow },
                                                                      { CheckState.Unchecked, TagStatus.None },
                                                                      { CheckState.Indeterminate, TagStatus.Ignore }};
        private static readonly Dictionary<TagStatus, CheckState> TagStatus2CheckState = new Dictionary<TagStatus, CheckState>
                                                                    { { TagStatus.Follow,CheckState.Checked },
                                                                      { TagStatus.None, CheckState.Unchecked },
                                                                      { TagStatus.Ignore, CheckState.Indeterminate }};
        public TagBox()
        {
            base.BackColor = System.Drawing.Color.Transparent;
            base.Margin = new System.Windows.Forms.Padding(0);
            base.FlowDirection = FlowDirection.TopDown;
            base.AutoScroll = true;
            base.WrapContents = false;
        }

        public void SetClient(BaseServer _client)
        {
            server = _client;
            tagsStatus = Task.Run(server.GetAllTagsStatus).ConfigureAwait(false).GetAwaiter().GetResult();
            tagsDesc = Task.Run(server.GetAllTagsDesc).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        private void SetTags(List<string> tags)
        {
            if (tags is null)
                return;
            blockSignal = true;
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
                if (tagsStatus.Keys.Contains(tag))
                    tag_group[TagStatus2CheckState[tagsStatus[tag]]].Add(tag);
                else
                    tag_group[CheckState.Unchecked].Add(tag);
            //按未关注->已关注->忽略的顺序
            var iterator = Controls.GetEnumerator();
            foreach (var state in new List<CheckState>{ CheckState.Unchecked,CheckState.Checked, CheckState.Indeterminate })
                foreach (var tag in tag_group[state])
                {
                    iterator.MoveNext();
                    if (tagsDesc.ContainsKey(tag) && !string.IsNullOrEmpty(tagsDesc[tag]))
                        ((CheckBox)iterator.Current).Text = String.Format("{0}`{1}", tag, tagsDesc[tag]);
                    else
                        ((CheckBox)iterator.Current).Text = tag;
                    ((CheckBox)iterator.Current).CheckState = state;
                    ((CheckBox)iterator.Current).Visible = true;
                }
            while (iterator.MoveNext())
                ((CheckBox)iterator.Current).Visible = false;            
            this.ResumeLayout();
            blockSignal = false;
        }
        private void ItemCheckHandler(object sender, EventArgs e)
        {
            if (blockSignal)
                return;
            var text = ((CheckBox)sender).Text;
            if (text.Count() == 0)
                return;
            if (text.Contains('`'))
                text = text.Substring(0,text.IndexOf('`'));
            var new_status = CheckState2TagStatus[((CheckBox)sender).CheckState];
            if (!tagsStatus.ContainsKey(text))
            {
                server.UpdateTagStatus(text, new_status);
                tagsStatus.Add(text, new_status);
            }
            else if(tagsStatus[text]!= new_status)
            {
                server.UpdateTagStatus(text, new_status);
                tagsStatus[text] = new_status;
            }
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            return true;
        }
    }
}
