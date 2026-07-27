using ColorVision.Engine.FlowProcessing.Diagnostics;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ColorVision.Engine.FlowProcessing.Editor
{
    internal sealed class FlowExecutionNavigator
    {
        private readonly STNodeEditor _nodeEditor;

        public FlowExecutionNavigator(STNodeEditor nodeEditor)
        {
            _nodeEditor = nodeEditor;
        }

        internal static CVCommonNode? ResolveExecutionNode(
            STNodeEditor nodeEditor,
            string? executionNodeName,
            CVCommonNode? preferredNode = null)
        {
            return ResolveExecutionNode(
                nodeEditor.Nodes.OfType<CVCommonNode>(),
                executionNodeName,
                preferredNode);
        }

        internal static CVCommonNode? ResolveExecutionNode(
            IEnumerable<CVCommonNode> nodes,
            string? executionNodeName,
            CVCommonNode? preferredNode = null)
        {
            if (string.IsNullOrWhiteSpace(executionNodeName))
                return null;

            List<CVCommonNode> candidates = nodes.ToList();
            if (preferredNode != null
                && candidates.Contains(preferredNode)
                && IsExecutionNodeNameMatch(preferredNode, executionNodeName))
            {
                return preferredNode;
            }

            var matches = candidates
                .Where(node => IsExecutionNodeNameMatch(node, executionNodeName))
                .Take(2)
                .ToList();
            return matches.Count == 1 ? matches[0] : null;
        }

        internal static bool IsExecutionNodeNameMatch(CVCommonNode node, string? executionNodeName)
        {
            if (string.IsNullOrWhiteSpace(executionNodeName))
                return false;

            string candidate = executionNodeName.Trim();
            string fullName = string.IsNullOrWhiteSpace(node.NodeName)
                ? node.Title
                : $"{node.Title}.{node.NodeName}";
            return string.Equals(node.NodeID, candidate, StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.NodeName, candidate, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fullName, candidate, StringComparison.OrdinalIgnoreCase);
        }

        public void OpenNodeExecutionDetails(CVCommonNode node, bool focusNode = false)
        {
            if (focusNode)
                FocusNode(node);

            var window = new FlowExecutionAnalysisWindow(
                node.NodeID,
                node.OnGetDrawTitle(),
                record => TryFocusExecutionNode(record.NodeId, record.NodeName))
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            window.Show();
        }

        public bool TryFocusExecutionNode(string? nodeId, string? nodeName = null)
        {
            CVCommonNode? node = ResolveExecutionNode(_nodeEditor, nodeId)
                ?? ResolveExecutionNode(_nodeEditor, nodeName);
            if (node == null)
                return false;

            FocusNode(node);
            return true;
        }

        private void FocusNode(STNode node)
        {
            foreach (STNode selectedNode in _nodeEditor.GetSelectedNode())
            {
                if (!ReferenceEquals(selectedNode, node))
                    selectedNode.SetSelected(bSelected: false, bRedraw: false);
            }

            _nodeEditor.SetActiveNode(node);
            float scale = _nodeEditor.CanvasScale;
            float offsetX = _nodeEditor.ClientSize.Width / 2f - (node.Left + node.Width / 2f) * scale;
            float offsetY = _nodeEditor.ClientSize.Height / 2f - (node.Top + node.Height / 2f) * scale;
            _nodeEditor.MoveCanvas(offsetX, offsetY, bAnimation: false, CanvasMoveArgs.All);
        }
    }
}
