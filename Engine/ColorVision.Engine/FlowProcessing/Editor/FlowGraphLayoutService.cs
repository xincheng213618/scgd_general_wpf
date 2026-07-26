using FlowEngineLib.Start;
using ST.Library.UI.NodeEditor;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.Editor
{
    internal sealed class FlowGraphLayoutService
    {
        private const int HorizontalSpacing = 220;
        private const int VerticalSpacing = 80;

        private readonly STNodeEditor _nodeEditor;

        public FlowGraphLayoutService(STNodeEditor nodeEditor)
        {
            _nodeEditor = nodeEditor;
        }

        public void Apply(int startX = 0, int startY = 0)
        {
            STNode? rootNode = _nodeEditor.Nodes.OfType<MQTTStartNode>().FirstOrDefault();
            if (rootNode == null)
                return;

            using var transaction = _nodeEditor.BeginEditTransaction("自动布局");
            var layout = new SugiyamaLayout(
                _nodeEditor.GetConnections(),
                startX,
                startY,
                HorizontalSpacing,
                VerticalSpacing,
                _nodeEditor.ClientSize.Width,
                _nodeEditor.ClientSize.Height);
            layout.Execute(rootNode);
        }

        public void FitToViewport()
        {
            _nodeEditor.FitCanvasToNodes(0.85f);
        }
    }
}
