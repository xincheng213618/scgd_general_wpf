using FlowEngineLib.End;
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
            ConnectionInfo[] connections = nodeEditor.GetConnectionInfo();
            log.Debug($"CheckFlow: 节点数={nodeEditor.Nodes.Count}, 连接数={connections.Length}");

            STNode? startNode = nodeEditor.Nodes.OfType<MQTTStartNode>().FirstOrDefault();
            if (startNode == null)
            {
                log.Warn("CheckFlow: 找不到流程起始结点 (MQTTStartNode)");
                MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.Flow_NoStartNode);
                return false;
            }

            STNode? endNode = nodeEditor.Nodes.OfType<CVEndNode>().FirstOrDefault();
            if (endNode == null)
            {
                log.Warn("CheckFlow: 找不到流程结束结点 (CVEndNode)");
                MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.Flow_NoEndNode);
                return false;
            }

            if (!IsPathExists(startNode, endNode, connections))
            {
                log.Warn("CheckFlow: 无法找到从起始结点到结束结点的有效路径");
                MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.Flow_NoPathFromStartToEnd);
                return false;
            }

            log.Debug("CheckFlow: 验证成功");
            return true;
        }

        private static bool IsPathExists(STNode startNode, STNode endNode, ConnectionInfo[] connections)
        {
            var children = connections
                .GroupBy(connection => connection.Output.Owner)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(connection => connection.Input.Owner).Distinct().ToArray());
            var visited = new HashSet<STNode>();
            var queue = new Queue<STNode>();
            queue.Enqueue(startNode);

            while (queue.Count > 0)
            {
                STNode current = queue.Dequeue();
                if (ReferenceEquals(current, endNode))
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
