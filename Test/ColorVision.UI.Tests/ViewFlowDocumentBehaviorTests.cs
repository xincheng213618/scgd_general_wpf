#pragma warning disable CA1707
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using System.Windows;

namespace ColorVision.UI.Tests;

public class ViewFlowDocumentBehaviorTests
{
    [Fact]
    public void MissingTemplateOpensAnEmptyDocumentWithoutTemplateOperations()
    {
        WpfTestHost.Invoke(() =>
        {
            var previous = ConfigService.Instance;
            ConfigService.SetInstance(new ConfigHandler());
            var theme = new ResourceDictionary { Source = new Uri("/ColorVision.Themes;component/Themes/Theme.xaml", UriKind.Relative) };
            Application.Current.Resources.MergedDictionaries.Add(theme);
            FlowEngineToolWindow? window = null;
            try
            {
                window = new FlowEngineToolWindow(TemplateFlow.GetParamOrDefault(-1))
                {
                    ShowActivated = false,
                    Left = -10000,
                    Top = -10000,
                };
                window.Show();
                Assert.True(window.View.IsStandalone);
                Assert.Equal(0, window.View.STNodeEditorMain.Nodes.Count);
                Assert.Null(window.View.GetStandaloneExecutionTemplate());
                Assert.False(window.View.DeleteFlowCommand.CanExecute(null));
                Assert.False(window.View.VersionHistoryCommand.CanExecute(null));
                Assert.False(window.View.ExportFlowCommand.CanExecute(null));
            }
            finally
            {
                window?.Close();
                Application.Current.Resources.MergedDictionaries.Remove(theme);
                ConfigService.SetInstance(previous);
            }
        });
    }

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
