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
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    class QueueTreeView:TreeView
    {
        public event EventHandler<QueueChangeEventArgs> QueueChanged;
        private List<BaseServer> providers;
        private bool blockSignal = false;
        private int updateVersion;
        public QueueTreeView()
        {
            HideSelection = false;
            ShowNodeToolTips = true;
            TabStop = false;
            BeforeSelect += (object sender, TreeViewCancelEventArgs e) =>
            {
                if (e.Node.Tag is not QueueChangeEventArgs)
                    e.Cancel = true;
            };
            NodeMouseClick += (object sender, TreeNodeMouseClickEventArgs e) =>
            {
                if (e.Button == MouseButtons.Left && e.Clicks == 1 && e.Node.Tag == null
                    && (HitTest(e.Location).Location & (TreeViewHitTestLocations.Label | TreeViewHitTestLocations.Image)) != 0)
                    e.Node.Toggle();
            };
            AfterSelect += (object sender, TreeViewEventArgs e) =>
            {
                if (!blockSignal && e.Node.Tag is QueueChangeEventArgs item)
                    QueueChanged?.Invoke(this, item);
            };
        }
        public void SetClient(List<BaseServer> _providers)
        {
            providers = _providers;
            UpdateContent();
        }
        public async void UpdateContent()
        {
            int version = ++updateVersion;
            var roots = new List<TreeNode>();
            var nodes = new Dictionary<string, TreeNode>();
            for(int i=0;i<providers.Count; i++)
            {
                var queues = await providers[i].GetExplorerQueues();
                if (IsDisposed || version != updateVersion)
                    return;
                if (queues.Count == 0)
                    continue;
                var module = providers[i].GetType().Namespace.Split('.')[^1];
                if (module == "LocalSingleFile")
                    module = "LSF";
                else if (module == "Twitter")
                    module = "X";
                var root = new TreeNode(module) { Name = $"module/{i}" };
                nodes.Add(root.Name, root);
                foreach (var item in queues)
                {
                    var node = new TreeNode(item.displayText)
                    {
                        Name = $"queue/{i}/{(int)item.type}/{item.id}",
                        ToolTipText = item.displayText,
                        Tag = new QueueChangeEventArgs(i, item)
                    };
                    nodes.Add(node.Name, node);
                    root.Nodes.Add(node);
                }
                roots.Add(root);
            }

            var expanded = new HashSet<string>();
            foreach (TreeNode root in Nodes)
                if (root.IsExpanded)
                    expanded.Add(root.Name);
            if (Nodes.Count == 0 && roots.Count > 0)
                expanded.Add(roots[0].Name);
            var selected = SelectedNode?.Name;
            var top = TopNode?.Name;
            BeginUpdate();
            blockSignal = true;
            try
            {
                Nodes.Clear();
                Nodes.AddRange(roots.ToArray());
                foreach (var key in expanded)
                    if (nodes.TryGetValue(key, out var node))
                        node.Expand();
                if (selected != null && nodes.TryGetValue(selected, out var selectedNode))
                    SelectedNode = selectedNode;
                if (top != null && nodes.TryGetValue(top, out var topNode))
                    TopNode = topNode;
            }
            finally
            {
                blockSignal = false;
                EndUpdate();
            }
        }
    }
}
