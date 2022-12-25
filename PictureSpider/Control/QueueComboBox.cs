using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using PictureSpider;

namespace PictureSpider
{
    public class QueueChangeEventArgs : EventArgs
    {
        public int server_index;
        public ExplorerQueue item;
        public QueueChangeEventArgs(int _idx, ExplorerQueue _item)
        {
            server_index = _idx;
            item = _item;
        }
    }
    class ComboBoxItem
    {
        public int provider_index;
        public ExplorerQueue item;
        public ComboBoxItem(int _idx, ExplorerQueue _item)
        {
            provider_index = _idx;
            item = _item;
        }
        public override string ToString()
        {
            return item.displayText;
        }
    };
    class QueueComboBox:ComboBox
    {
        public event EventHandler<QueueChangeEventArgs> QueueChanged;
        List<BaseServer> providers;
        bool block_signal = false;
        public QueueComboBox()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            SelectedIndexChanged += (object sender, EventArgs e) =>
            {
                if (!block_signal)
                    if(SelectedItem!=null)
                        QueueChanged(this, new QueueChangeEventArgs(((ComboBoxItem)SelectedItem).provider_index, ((ComboBoxItem)SelectedItem).item));
            };
        }
        public void SetClient(List<BaseServer> _providers)
        {
            providers = _providers;
            UpdateContent();
        }
        public async void UpdateContent()
        {
            var old_select = SelectedItem as ComboBoxItem;
            Items.Clear();
            using (BlockSyncContext.Enter())
                for(int i=0;i<providers.Count; i++)
                foreach (var item in await providers[i].GetExplorerQueues())
                    Items.Add(new ComboBoxItem(i,item));
            if(old_select!= null)//重新选中之前选中的选项
            {
                block_signal = true;
                foreach (ComboBoxItem item in Items)
                    if (item.provider_index == old_select.provider_index
                        &&item.item.type== old_select.item.type
                        &&item.item.id== old_select.item.id)
                        {
                            SelectedItem = item;
                            block_signal = false;
                            return;
                        }
                block_signal = false;
                //即使选中的队列已不存在，也没有必要清空
            }
        }
    }
}