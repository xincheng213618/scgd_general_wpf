#pragma warning disable CA1707
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.Templates.Flow;

namespace ColorVision.UI.Tests;

public class ViewFlowDocumentBehaviorTests
{
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
