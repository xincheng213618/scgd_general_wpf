#pragma warning disable CA1707
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.Templates.Flow;

namespace ColorVision.UI.Tests;

public class ViewFlowDocumentBehaviorTests
{
    [Fact]
    public void DisplayFlowDoesNotExposeExecutionCommands()
    {
        Assert.Null(typeof(DisplayFlow).GetMethod("RunFlow"));
        Assert.Null(typeof(DisplayFlow).GetMethod("RunFlowAsync"));
        Assert.Null(typeof(DisplayFlow).GetMethod("RunFlowAndWaitAsync"));
        Assert.Null(typeof(DisplayFlow).GetMethod("StopFlow"));
        Assert.Null(typeof(DisplayFlow).GetMethod("Refresh"));
        Assert.Null(typeof(DisplayFlow).GetMethod("RefreshAsync"));
        Assert.NotNull(typeof(FlowEngineManager).GetMethod(nameof(FlowEngineManager.RunFlowAsync)));
    }

    [Fact]
    public void ExistingViewFlowDocumentMethodsKeepVoidReturnTypes()
    {
        Assert.Equal(
            typeof(void),
            typeof(ViewFlow).GetMethod(nameof(ViewFlow.Save), Type.EmptyTypes)!.ReturnType);
        Assert.Equal(
            typeof(void),
            typeof(ViewFlow).GetMethod(
                nameof(ViewFlow.OpenStandaloneFile),
                [typeof(string)])!.ReturnType);
        Assert.Equal(
            typeof(void),
            typeof(ViewFlow).GetMethod(
                nameof(ViewFlow.OpenStandaloneFlowParam),
                [typeof(FlowParam), typeof(bool)])!.ReturnType);
    }
}
