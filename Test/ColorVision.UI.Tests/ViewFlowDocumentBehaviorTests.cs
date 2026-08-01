#pragma warning disable CA1707
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.Templates.Flow;
using System.Windows;

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

    [Theory]
    [InlineData(true, 1, 0)]
    [InlineData(false, 0, 1)]
    public void NewCommandRoutesToOneDocumentAction(
        bool isStandalone,
        int expectedDocumentCalls,
        int expectedTemplateCalls)
    {
        int documentCalls = 0;
        int templateCalls = 0;

        FlowNewCommandRouter.Execute(
            isStandalone,
            () => documentCalls++,
            () => templateCalls++);

        Assert.Equal(expectedDocumentCalls, documentCalls);
        Assert.Equal(expectedTemplateCalls, templateCalls);
    }

    [Theory]
    [InlineData(MessageBoxResult.No, true, false)]
    [InlineData(MessageBoxResult.Cancel, false, false)]
    [InlineData(MessageBoxResult.Yes, true, true)]
    [InlineData(MessageBoxResult.Yes, false, false)]
    public void ModifiedDocumentReplacementHonorsDecisionAndSave(
        MessageBoxResult decision,
        bool expected,
        bool saveResult)
    {
        bool saveCalled = false;

        bool result = FlowDocumentReplacementGuard.Confirm(
            isModified: true,
            () => decision,
            () =>
            {
                saveCalled = true;
                return saveResult;
            });

        Assert.Equal(expected, result);
        Assert.Equal(
            decision == MessageBoxResult.Yes,
            saveCalled);
    }

    [Fact]
    public void CleanDocumentReplacementSkipsPromptAndSave()
    {
        bool result = FlowDocumentReplacementGuard.Confirm(
            isModified: false,
            () => throw new InvalidOperationException(),
            () => throw new InvalidOperationException());

        Assert.True(result);
    }
}
