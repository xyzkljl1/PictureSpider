using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using PixivAss;
using PixivAss.Data;

namespace PixivAss
{
    public class QueueChangeEventArgs : EventArgs
    {
        public int name;
        public ExploreQueueType type;
        public QueueChangeEventArgs(ExploreQueueType _type, int _name)
        {
            type = _type;
            name = _name;
        }
    }
    class QueueComboBox:ComboBox
    {
        public event EventHandler<QueueChangeEventArgs> QueueChanged;
        Client client;
        bool block_signal = false;
        public QueueComboBox()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            this.SelectedIndexChanged += (object sender, EventArgs e) =>
            {
                if (!block_signal)
                    QueueChanged(this, new QueueChangeEventArgs(((ComboBoxItem)SelectedItem).type, ((ComboBoxItem)SelectedItem).id));
            };
        }
        public void SetClient(Client _client)
        {
            client = _client;
            UpdateContent();
        }
        public async void UpdateContent()
        {
            var old_select = this.SelectedItem;
            Items.Clear();
            using (BlockSyncContext.Enter())
                foreach (var item in await client.GetExploreQueueName())
                    Items.Add(new ComboBoxItem(item));
            if(old_select!= null)
            {
                block_signal = true;
                foreach (var item in Items)
                    if (((ComboBoxItem)item).type == ((ComboBoxItem)old_select).type)
                        if (((ComboBoxItem)item).id == ((ComboBoxItem)old_select).id)
                        {
                            this.SelectedItem = item;
                            return;
                        }
                block_signal = false;
                //即使选中的队列已不存在，也没有必要清空
            }
        }
    }
    struct ComboBoxItem
    {
        public int id;
        public string name;
        public ExploreQueueType type;
        public ComboBoxItem(Tuple<ExploreQueueType, int, string> data)
        {
            this.type = data.Item1;
            this.id = data.Item2;
            this.name = data.Item3;
        }
        public override string ToString()
        {
            if (type == ExploreQueueType.Fav)
                return "Fav";
            else if (type == ExploreQueueType.FavR)
                return "FavR";
            else if (type == ExploreQueueType.Main)
                return "Main";
            else if (type == ExploreQueueType.MainR)
                return "MainR";
            else
                return name;
        }
    };
}