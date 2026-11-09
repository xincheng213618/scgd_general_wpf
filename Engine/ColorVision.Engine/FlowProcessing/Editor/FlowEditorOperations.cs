using ST.Library.UI.NodeEditor;
using System;

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
    }
}
