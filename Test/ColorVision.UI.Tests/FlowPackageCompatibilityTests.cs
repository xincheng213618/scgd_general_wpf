using ColorVision.Engine.Templates.Flow;
using ST.Library.UI.NodeEditor;
using System.Runtime.ExceptionServices;

namespace ColorVision.UI.Tests;

public class FlowPackageCompatibilityTests
{
    [Fact]
    public void TemplateExtractionAndReplacementAcceptVersionOne()
    {
        RunInSta(() =>
        {
            byte[] flowData = CreateTemplateReferenceFlow("Camera.Template.A");

            Assert.Contains("Camera.Template.A", FlowPackageHelper.ExtractTemplateNames(flowData));

            byte[] replaced = FlowPackageHelper.ReplaceTemplateNames(
                flowData,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Camera.Template.A"] = "Camera.Template.B",
                });

            Assert.DoesNotContain("Camera.Template.A", FlowPackageHelper.ExtractTemplateNames(replaced));
            Assert.Contains("Camera.Template.B", FlowPackageHelper.ExtractTemplateNames(replaced));
            Assert.Equal(1, replaced[4]);
        });
    }

    [Fact]
    public void UnknownStnVersionIsRejectedWithoutRewritingInput()
    {
        RunInSta(() =>
        {
            byte[] flowData = CreateTemplateReferenceFlow("Camera.Template.A");
            flowData[4] = 2;
            byte[] original = flowData.ToArray();

            Assert.Empty(FlowPackageHelper.ExtractTemplateNames(flowData));

            byte[] replacementResult = FlowPackageHelper.ReplaceTemplateNames(
                flowData,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Camera.Template.A"] = "Camera.Template.B",
                });

            Assert.Same(flowData, replacementResult);
            Assert.Equal(original, replacementResult);
        });
    }

    private static byte[] CreateTemplateReferenceFlow(string templateName)
    {
        using var editor = new STNodeEditor();
        var node = new TemplateReferenceNode
        {
            TempName = templateName,
        };
        node.Create();
        editor.Nodes.Add(node);
        return editor.GetCanvasData();
    }

    private static void RunInSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception != null)
            ExceptionDispatchInfo.Capture(exception).Throw();
    }

    private sealed class TemplateReferenceNode : STNode
    {
        [STNodeProperty("Template", "Template reference")]
        public string TempName { get; set; } = string.Empty;
    }
}
