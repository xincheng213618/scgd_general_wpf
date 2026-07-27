#pragma warning disable CA1707
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.Templates.Flow;

namespace ColorVision.UI.Tests;

public class ViewFlowDocumentBehaviorTests
{
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, true)]
    public void SavePromptRequiresStandaloneDirtyDocumentAndEnabledSetting(
        bool isStandalone,
        bool hasChanges,
        bool editSavePromptEnabled,
        bool expected)
    {
        Assert.Equal(
            expected,
            ViewFlow.ShouldConfirmStandaloneDocumentReplacement(
                isStandalone,
                hasChanges,
                editSavePromptEnabled));
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
