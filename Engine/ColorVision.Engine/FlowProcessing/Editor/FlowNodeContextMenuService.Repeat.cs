using FlowEngineLib.Base;
using FlowEngineLib.Repeat;
using FlowEngineLib.Start;
using ST.Library.UI.NodeEditor;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.FlowProcessing.Editor;

internal sealed partial class FlowNodeContextMenuService
{
    private bool CanEditRepeat => _nodeEditor.EnableEdit && !_nodeEditor.Nodes.OfType<BaseStartNode>().Any(node => node.Running);

    private void AddRepeatMenuItems(ItemCollection items, STNode? clicked)
    {
        CVCommonNode? member = clicked as CVCommonNode;
        if (clicked == null)
            member = _nodeEditor.GetNodeGroups().FirstOrDefault(group => group.Bounds.Contains(_contextCanvasPoint))?.Nodes.OfType<CVCommonNode>().FirstOrDefault();
        if (member != null && !string.IsNullOrEmpty(member.RepeatRegionId))
        {
            CVCommonNode[] members = _nodeEditor.Nodes.OfType<CVCommonNode>().Where(node => node.RepeatRegionId == member.RepeatRegionId).ToArray();
            var edit = new MenuItem { Header = FlowRepeatRegion.Text("修改重复次数…"), IsEnabled = CanEditRepeat };
            edit.Click += (_, _) => EditRepeat(members, member.RepeatRegionId, member.RepeatCount);
            items.Add(edit);
            var remove = new MenuItem { Header = FlowRepeatRegion.Text("取消重复框"), IsEnabled = CanEditRepeat };
            remove.Click += (_, _) =>
            {
                if (!CanEditRepeat) return;
                _nodeEditor.ExecuteEditTransaction(FlowRepeatRegion.Text("取消重复框"), () =>
                {
                    foreach (CVCommonNode node in members) node.SetRepeatRegion(string.Empty, 1, 0);
                });
            };
            items.Add(remove);
        }
        else
        {
            STNode[] selected = clicked != null && !clicked.IsSelected ? new[] { clicked } : _nodeEditor.GetSelectedNode();
            if (selected.Length == 0) return;
            var create = new MenuItem { Header = FlowRepeatRegion.Text("重复执行…"), IsEnabled = CanEditRepeat };
            create.Click += (_, _) =>
            {
                if (!CanEditRepeat) return;
                if (selected.OfType<CVCommonNode>().Any(node => !string.IsNullOrEmpty(node.RepeatRegionId)))
                {
                    MessageBox.Show(Window.GetWindow(_nodeEditor), FlowRepeatRegion.Text("选中的节点已经属于重复框，请先取消原来的重复框。"));
                    return;
                }
                if (!FlowRepeatRegion.TryCreate(selected, out FlowRepeatRegion region, out string error))
                {
                    MessageBox.Show(Window.GetWindow(_nodeEditor), error);
                    return;
                }
                EditRepeat(region.Nodes, Guid.NewGuid().ToString(), 10);
            };
            items.Add(create);
        }
        items.Add(new Separator());
    }

    private void EditRepeat(CVCommonNode[] members, string id, int count)
    {
        if (!CanEditRepeat) return;
        var dialog = new FlowRepeatCountWindow(count)
        {
            Owner = Window.GetWindow(_nodeEditor)
        };
        if (dialog.ShowDialog() == true && CanEditRepeat)
        {
            _nodeEditor.ExecuteEditTransaction(FlowRepeatRegion.Text("重复执行…"), () =>
            {
                foreach (CVCommonNode node in members) node.SetRepeatRegion(id, dialog.Count, members.Length);
            });
        }
    }
}
