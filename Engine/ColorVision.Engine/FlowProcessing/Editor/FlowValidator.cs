using FlowEngineLib.Base;
using FlowEngineLib.End;
using FlowEngineLib.Logical;
using FlowEngineLib.Start;
using log4net;
using ST.Library.UI.NodeEditor;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ColorVision.Engine.FlowProcessing.Editor
{
    internal static class FlowValidator
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FlowValidator));

        public static bool Validate(STNodeEditor nodeEditor)
        {
            if (TryValidate(nodeEditor, out string error, out STNode? invalidStart))
            {
                log.Debug("CheckFlow: 验证成功");
                return true;
            }

            if (invalidStart != null)
                new FlowExecutionNavigator(nodeEditor).TryFocusNode(invalidStart.Guid);
            log.Warn($"CheckFlow: {error}");
            MessageBox.Show(Application.Current.GetActiveWindow(), error);
            return false;
        }

        internal static bool TryValidate(STNodeEditor nodeEditor, out string error, out STNode? invalidStart)
        {
            invalidStart = null;
            if (!FlowEngineLib.Repeat.FlowRepeatRegion.TryValidate(nodeEditor.Nodes.Cast<STNode>(), out error))
                return false;

            ConnectionInfo[] connections = nodeEditor.GetConnectionInfo();
            log.Debug($"CheckFlow: 节点数={nodeEditor.Nodes.Count}, 连接数={connections.Length}");

            STNode[] startNodes = nodeEditor.Nodes.OfType<BaseStartNode>().ToArray();
            if (startNodes.Length == 0)
            {
                error = Properties.Resources.Flow_NoStartNode;
                return false;
            }

            var endNodes = new HashSet<STNode>(nodeEditor.Nodes.Cast<STNode>().Where(node => node is CVEndNode or FlowTerminationNode));
            if (endNodes.Count == 0)
            {
                error = Properties.Resources.Flow_NoEndNode;
                return false;
            }

            if (!IsPathExists(startNodes, endNodes, connections))
            {
                error = Properties.Resources.Flow_NoPathFromStartToEnd;
                invalidStart = startNodes.Length == 1 ? startNodes[0] : null;
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsPathExists(IEnumerable<STNode> startNodes, HashSet<STNode> endNodes, ConnectionInfo[] connections)
        {
            var children = connections
                .Where(connection => connection.Output.DataType == typeof(CVStartCFC) && connection.Input.DataType == typeof(CVStartCFC))
                .GroupBy(connection => connection.Output.Owner)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(connection => connection.Input.Owner).Distinct().ToArray());
            var visited = new HashSet<STNode>();
            var queue = new Queue<STNode>(startNodes);

            while (queue.Count > 0)
            {
                STNode current = queue.Dequeue();
                if (endNodes.Contains(current))
                    return true;
                if (!visited.Add(current) || !children.TryGetValue(current, out STNode[]? nextNodes))
                    continue;

                foreach (STNode child in nextNodes)
                {
                    if (!visited.Contains(child))
                        queue.Enqueue(child);
                }
            }

            return false;
        }
    }
}
