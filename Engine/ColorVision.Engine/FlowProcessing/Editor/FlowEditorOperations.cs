using ST.Library.UI.NodeEditor;
using System;
using System.Windows.Input;

namespace ColorVision.Engine.FlowProcessing.Editor
{
    internal static class FlowEditorOperations
    {
        public static void ClearSelection(STNodeEditor nodeEditor)
        {
            ArgumentNullException.ThrowIfNull(nodeEditor);

            nodeEditor.SetActiveNode(null);
            foreach (STNode node in nodeEditor.GetSelectedNode())
            {
                node.SetSelected(bSelected: false, bRedraw: false);
            }
            nodeEditor.Invalidate();
        }

        public static void ImportCanvasAsModule(STNodeEditor nodeEditor, byte[] canvasData)
        {
            ArgumentNullException.ThrowIfNull(nodeEditor);
            ArgumentNullException.ThrowIfNull(canvasData);

            System.Drawing.Point target;
            if (nodeEditor.IsMouseOver)
            {
                var mousePosition = Mouse.GetPosition(nodeEditor);
                target = nodeEditor.ControlToCanvas(new System.Drawing.Point(
                    (int)Math.Round(mousePosition.X),
                    (int)Math.Round(mousePosition.Y)));
            }
            else
            {
                target = nodeEditor.ControlToCanvas(new System.Drawing.Point(
                    nodeEditor.ClientSize.Width / 2,
                    nodeEditor.ClientSize.Height / 2));
            }
            nodeEditor.ImportCanvasAsModule(canvasData, target);
        }
    }
}
