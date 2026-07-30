using ColorVision.Engine.Templates.Flow;
using ST.Library.UI.NodeEditor;
using System;
using System.Windows;

namespace ColorVision.Engine.FlowProcessing.Compilation;

public static class FlowSubflowEditorCommand
{
    public static bool CanOpen(
        FlowParam? flowParam,
        bool isFlowRunning)
    {
        return !isFlowRunning
            && flowParam != null
            && !string.IsNullOrWhiteSpace(flowParam.FlowKey)
            && !string.IsNullOrWhiteSpace(flowParam.DataBase64);
    }

    public static bool? Open(
        FlowParam flowParam,
        STNodeEditor editor,
        Window? owner = null)
    {
        ArgumentNullException.ThrowIfNull(flowParam);
        ArgumentNullException.ThrowIfNull(editor);
        var window = new FlowSubflowEditorWindow(
            flowParam,
            editor)
        {
            Owner = owner,
        };
        return window.ShowDialog();
    }
}
