using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace PictureSpider
{
    public class QueueChangeEventArgs(int idx, ExplorerQueue item) : EventArgs
    {
        public readonly int ServerIndex = idx;
        public ExplorerQueue Item = item;
    }
    class ComboBoxItem(int idx, ExplorerQueue item)
    {
        public readonly int ProviderIndex = idx;
        public ExplorerQueue Item = item;

        public override string ToString()
        {
            return Item.displayText;
        }
    }; 
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    class QueueComboBox:ComboBox
    {
        public event EventHandler<QueueChangeEventArgs> QueueChanged;
        private List<BaseServer> providers;
        private bool blockSignal = false;
        public QueueComboBox()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            SelectedIndexChanged += (object sender, EventArgs e) =>
            {
                if (!blockSignal)
                    if(SelectedItem!=null)
                        QueueChanged?.Invoke(this, new QueueChangeEventArgs(((ComboBoxItem)SelectedItem).ProviderIndex, ((ComboBoxItem)SelectedItem).Item));
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
            for(int i=0;i<providers.Count; i++)
                foreach (var item in await providers[i].GetExplorerQueues())
                    Items.Add(new ComboBoxItem(i,item));
            if(old_select!= null)//重新选中之前选中的选项
            {
                blockSignal = true;
                foreach (ComboBoxItem item in Items)
                    if (item.ProviderIndex == old_select.ProviderIndex
                        &&item.Item.type== old_select.Item.type
                        &&item.Item.id== old_select.Item.id)
                    {
                        SelectedItem = item;
                        blockSignal = false;
                        return;
                    }
                blockSignal = false;
                //即使选中的队列已不存在，也没有必要清空
            }
        }
    }
}