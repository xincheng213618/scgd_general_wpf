#pragma warning disable CA1707
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Flow.Versioning;
using ColorVision.UI;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public class ViewFlowDocumentBehaviorTests
{
    [Fact]
    public void ConfirmedStandaloneVersionRestoreReplacesDirtyCanvasAndRepeatParameters()
    {
        WithStandaloneWindow(window =>
        {
            ViewFlow view = window.View;
            var param = new FlowParam { Name = "Repeat history", FlowKey = "flow:standalone-repeat", DataBase64 = Convert.ToBase64String(CreateCanvas("Old canvas")) };
            view.LoadStandaloneFlowParam(param, true);
            view.STNodeEditorMain.Nodes[0].Title = "Unsaved draft";
            Assert.True(view.STNodeEditorMain.IsModified);

            using var historical = new STNodeEditor();
            var node = new LocalSmuNode();
            node.Create();
            historical.Nodes.Add(node);
            node.SetRepeatRegion("standalone-repeat", 3, 1);
            node.SetRepeatParameterValues(new Dictionary<string, double[]>
            {
                [nameof(LocalSmuNode.MeasureValue)] = [-1.2, -1.3, -1.4],
            });
            byte[] restoredCanvas = historical.GetCanvasData();
            param.DataBase64 = Convert.ToBase64String(restoredCanvas);
            param.LoadedContentHash = FlowSemanticHash.ComputeBinaryHash(restoredCanvas);
            PumpUntil(view.ReloadRestoredVersionAsync(param));

            LocalSmuNode loaded = Assert.Single(view.STNodeEditorMain.Nodes.OfType<LocalSmuNode>());
            Assert.Equal("standalone-repeat", loaded.RepeatRegionId);
            Assert.Equal(3, loaded.RepeatCount);
            Assert.Equal(new double[] { -1.2, -1.3, -1.4 }, loaded.RepeatParameterValues[nameof(LocalSmuNode.MeasureValue)]);
            Assert.Single(view.STNodeEditorMain.GetNodeGroups());
            Assert.Equal(param.LoadedContentHash, GetField(view, "_documentLoadedContentHash"));
            Assert.False(view.STNodeEditorMain.IsModified);
            Assert.False(view.STNodeEditorMain.CanUndo);
        });
    }

    [Fact]
    public void StandaloneFailureDetailsAreVisibleOnlyForAnIdentifiedCurrentNode()
    {
        WithStandaloneWindow(window =>
        {
            ViewFlow view = window.View;
            var first = new CVDeviceNode("FOV", "SVR", "same", string.Empty);
            var failed = new CVDeviceNode("FOV", "SVR", "same", string.Empty);
            foreach (CVCommonNode node in new[] { first, failed }) { node.Create(); view.STNodeEditorMain.Nodes.Add(node); }
            view.STNodeEditorMain.ClearHistory();
            var details = (Button)view.FindName("ErrorNodeDetailsButton");

            view.ShowExecutionSummary("Failed", failed.NodeID, first);
            Assert.Equal(Visibility.Visible, details.Visibility);
            Assert.Same(failed, GetField(view, "_executionDetailsNode"));
            Assert.NotNull(details.ToolTip);
            Assert.False(view.STNodeEditorMain.IsModified);

            view.ShowExecutionSummary("Failed", $"{first.Title}.{first.NodeName}");
            Assert.Equal(Visibility.Collapsed, details.Visibility);
            Assert.Null(GetField(view, "_executionDetailsNode"));

            view.STNodeEditorMain.Nodes.Remove(failed);
            view.ShowExecutionSummary("Failed", failed.NodeID, failed);
            Assert.Equal(Visibility.Collapsed, details.Visibility);
            Assert.Null(details.ToolTip);
            Assert.Null(GetField(view, "_executionDetailsNode"));

            view.ShowExecutionSummary("Legacy failure", $"{first.Title}.{first.NodeName}");
            Assert.Equal(Visibility.Visible, details.Visibility);
            Assert.Same(first, GetField(view, "_executionDetailsNode"));
            view.ShowExecutionSummary("Completed");
            Assert.Equal(Visibility.Collapsed, details.Visibility);
            Assert.Null(GetField(view, "_executionDetailsNode"));
        });
    }

    [Theory]
    [InlineData(false, "missing-file")]
    [InlineData(false, "corrupt-file")]
    [InlineData(false, "corrupt-package")]
    [InlineData(true, "bad-base64")]
    [InlineData(true, "corrupt-template")]
    [InlineData(true, "corrupt-file")]
    public void FailedStandaloneOpenPreservesDocumentIdentityDirtyStateAndHistory(bool originalIsTemplate, string failure)
    {
        WithStandaloneWindow(window =>
        {
            ViewFlow view = window.View;
            STNodeEditor editor = view.STNodeEditorMain;
            string originalPath = Path.Combine(Path.GetTempPath(), $"flow-original-{Guid.NewGuid():N}.stn");
            string brokenPath = Path.Combine(Path.GetTempPath(), $"flow-broken-{Guid.NewGuid():N}{(failure == "corrupt-package" ? ".cvflow" : ".stn")}");
            try
            {
                byte[] validCanvas = CreateCanvas("Original");
                File.WriteAllBytes(originalPath, validCanvas);
                if (originalIsTemplate)
                    view.LoadStandaloneFlowParam(new FlowParam { Name = "Original template", DataBase64 = Convert.ToBase64String(validCanvas) }, true);
                else
                    view.LoadStandaloneFile(originalPath);

                STNode original = Assert.Single(editor.Nodes.Cast<STNode>());
                editor.ExecuteEditTransaction("Rename", () => original.Title = "Unsaved original");
                var extra = new STNodeHub();
                extra.Create();
                editor.Nodes.Add(extra);
                editor.Undo();
                Assert.True(editor.IsModified);
                Assert.True(editor.CanUndo);
                Assert.True(editor.CanRedo);
                var undoHistory = editor.UndoHistory.ToArray();
                var redoHistory = editor.RedoHistory.ToArray();
                byte[] currentCanvas = editor.GetCanvasData();
                string title = window.Title;
                string[] identityFields = ["_standaloneFlowParam", "_standaloneFilePath", "_standaloneDocumentName", "_saveStandaloneFlowParam", "_documentLoadedContentHash", "_standaloneStartNodeName"];
                var identity = identityFields.ToDictionary(name => name, name => GetField(view, name));

                // Choosing Discard authorizes replacement; a failed target must still leave this document intact.
                Assert.True(FlowDocumentReplacementGuard.Confirm(editor.IsModified, () => MessageBoxResult.No, () => throw new InvalidOperationException()));
                byte[] corruptCanvas = validCanvas[..^4];
                if (failure != "missing-file")
                    File.WriteAllBytes(brokenPath, corruptCanvas);
                if (failure is "bad-base64" or "corrupt-template")
                {
                    var invalid = new FlowParam
                    {
                        Name = "Broken template",
                        DataBase64 = failure == "bad-base64" ? "invalid base64" : Convert.ToBase64String(corruptCanvas)
                    };
                    Assert.ThrowsAny<Exception>(() => view.LoadStandaloneFlowParam(invalid, false));
                }
                else
                {
                    Assert.ThrowsAny<Exception>(() => view.LoadStandaloneFile(brokenPath));
                }

                Assert.Same(original, Assert.Single(editor.Nodes.Cast<STNode>()));
                Assert.Equal(currentCanvas, editor.GetCanvasData());
                Assert.Equal(title, window.Title);
                foreach (string field in identityFields)
                    Assert.Equal(identity[field], GetField(view, field));
                Assert.True(editor.IsModified);
                Assert.Equal(undoHistory, editor.UndoHistory);
                Assert.Equal(redoHistory, editor.RedoHistory);
                editor.Redo();
                Assert.Contains(extra, editor.Nodes.Cast<STNode>());
                editor.Undo();
                editor.Undo();
                Assert.Equal("Original", original.Title);
                Assert.False(editor.IsModified);
            }
            finally
            {
                File.Delete(originalPath);
                File.Delete(brokenPath);
            }
        });
    }

    [Fact]
    public void FailedOpenKeepsOldNodeEventsAndSuccessfulOpenDetachesThem()
    {
        WithStandaloneWindow(window =>
        {
            ViewFlow view = window.View;
            var previousNode = new FlowEngineLib.End.CVEndNode();
            previousNode.Create();
            view.STNodeEditorMain.Nodes.Add(previousNode);
            object session = GetField(view, "_executionSession")!;
            session.GetType().GetMethod("AttachExecutionNodeEvents", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, null);
            var runHandler = previousNode.nodeRunEvent;
            var endHandler = previousNode.nodeEndEvent;
            Assert.NotNull(runHandler);
            Assert.NotNull(endHandler);

            const string oldSummary = "Previous document failed";
            const BindingFlags sessionMembers = BindingFlags.Instance | BindingFlags.NonPublic;
            Type sessionType = session.GetType();
            long generation = (long)sessionType.GetField("_executionGeneration", sessionMembers)!.GetValue(session)!;
            sessionType.GetField("_lastFailedNode", sessionMembers)!.SetValue(session, previousNode);
            sessionType.GetField("_completedErrorNodeKey", sessionMembers)!.SetValue(session, previousNode.NodeID);
            sessionType.GetField("_completedSummaryMessage", sessionMembers)!.SetValue(session, oldSummary);
            view.ShowExecutionSummary(oldSummary, previousNode.NodeID, previousNode);
            var details = (Button)view.FindName("ErrorNodeDetailsButton");
            var pendingWrite = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task delayedSummary = (Task)sessionType.GetMethod("ShowExecutionSummaryAfterNodeWritesAsync", sessionMembers)!.Invoke(
                session, [previousNode, pendingWrite.Task, previousNode.NodeID, oldSummary, generation])!;
            Assert.False(delayedSummary.IsCompleted);
            Assert.False(view.IsExecutionActive);

            try
            {
                byte[] canvas = CreateCanvas("Replacement");
                Assert.ThrowsAny<Exception>(() => view.LoadStandaloneFlowParam(new FlowParam
                {
                    Name = "Broken",
                    DataBase64 = Convert.ToBase64String(canvas[..^4])
                }, false));
                Assert.Equal(runHandler, previousNode.nodeRunEvent);
                Assert.Equal(endHandler, previousNode.nodeEndEvent);
                Assert.Equal(generation, (long)sessionType.GetField("_executionGeneration", sessionMembers)!.GetValue(session)!);
                Assert.Equal(oldSummary, view.logTextBox.Text);
                Assert.Equal(Visibility.Visible, details.Visibility);
                Assert.Same(previousNode, GetField(view, "_executionDetailsNode"));
                Assert.False(delayedSummary.IsCompleted);

                view.LoadStandaloneFlowParam(new FlowParam { Name = "Replacement", DataBase64 = Convert.ToBase64String(canvas) }, false);
                Assert.Null(previousNode.nodeRunEvent);
                Assert.Null(previousNode.nodeEndEvent);
                Assert.DoesNotContain(previousNode, view.STNodeEditorMain.Nodes.Cast<STNode>());
                Assert.Equal(string.Empty, view.logTextBox.Text);
                Assert.Equal(Visibility.Collapsed, details.Visibility);

                pendingWrite.SetResult(true);
                PumpUntil(delayedSummary);

                Assert.Equal("Replacement", Assert.Single(view.STNodeEditorMain.Nodes.Cast<STNode>()).Title);
                Assert.Equal(string.Empty, view.logTextBox.Text);
                Assert.Equal(Visibility.Collapsed, details.Visibility);
                Assert.Null(GetField(view, "_executionDetailsNode"));
                Assert.Null(details.ToolTip);
            }
            finally
            {
                pendingWrite.TrySetResult(true);
                PumpUntil(delayedSummary);
            }
        });
    }

    [Fact]
    public void SuccessfulStandaloneReloadAndReplacementCommitTheirOwnIdentity()
    {
        WithStandaloneWindow(window =>
        {
            ViewFlow view = window.View;
            string path = Path.Combine(Path.GetTempPath(), $"flow-reload-{Guid.NewGuid():N}.stn");
            try
            {
                File.WriteAllBytes(path, CreateCanvas("Saved file"));
                view.LoadStandaloneFile(path);
                STNode original = Assert.Single(view.STNodeEditorMain.Nodes.Cast<STNode>());
                original.Title = "Unsaved title";
                Assert.True(view.STNodeEditorMain.IsModified);
                view.LoadStandaloneFile(path);
                Assert.Equal("Saved file", Assert.Single(view.STNodeEditorMain.Nodes.Cast<STNode>()).Title);
                Assert.False(view.STNodeEditorMain.IsModified);
                Assert.False(view.STNodeEditorMain.CanUndo);

                var template = new FlowParam { Name = "Replacement template", DataBase64 = Convert.ToBase64String(CreateCanvas("Template node")) };
                view.LoadStandaloneFlowParam(template, true);
                Assert.Same(template, GetField(view, "_standaloneFlowParam"));
                Assert.Null(GetField(view, "_standaloneFilePath"));
                Assert.Equal(true, GetField(view, "_saveStandaloneFlowParam"));
                Assert.Equal("Template node", Assert.Single(view.STNodeEditorMain.Nodes.Cast<STNode>()).Title);
                Assert.Contains(template.Name, window.Title);
                Assert.False(view.STNodeEditorMain.IsModified);
                Assert.False(view.STNodeEditorMain.CanUndo);

                view.LoadStandaloneFile(path);
                Assert.Null(GetField(view, "_standaloneFlowParam"));
                Assert.Equal(path, GetField(view, "_standaloneFilePath"));
                Assert.Equal(false, GetField(view, "_saveStandaloneFlowParam"));
                Assert.Null(GetField(view, "_documentLoadedContentHash"));
                Assert.Contains(Path.GetFileName(path), window.Title);
            }
            finally { File.Delete(path); }
        });
    }

    private static object? GetField(ViewFlow view, string name)
        => typeof(ViewFlow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view);

    private static void PumpUntil(Task task)
    {
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            _ = task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)), TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }

    private static byte[] CreateCanvas(string title)
    {
        using var editor = new STNodeEditor();
        var node = new STNodeHub { Title = title };
        node.Create();
        editor.Nodes.Add(node);
        return editor.GetCanvasData();
    }

    private static void WithStandaloneWindow(Action<FlowEngineToolWindow> test)
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
                window = new FlowEngineToolWindow { ShowActivated = false, Left = -10000, Top = -10000 };
                window.Show();
                test(window);
            }
            finally
            {
                window?.View.STNodeEditorMain.MarkSaved();
                window?.Close();
                Application.Current.Resources.MergedDictionaries.Remove(theme);
                ConfigService.SetInstance(previous);
            }
        });
    }

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
