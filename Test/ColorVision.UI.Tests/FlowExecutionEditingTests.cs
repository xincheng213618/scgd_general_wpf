using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Engine.FlowProcessing.Integration;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.FlowProcessing.PostProcess;
using ColorVision.Engine.FlowProcessing.PreProcess;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using ColorVision.UI.Views;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using FlowEngineLib.Logical;
using FlowEngineLib.Start;
using ST.Library.UI.NodeEditor;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class FlowExecutionEditingTests
{
    [Fact]
    public void CopilotFlowSnapshotIdentityIsStableAcrossRevisionsAndDistinctForOtherHosts()
    {
        WithFixture(fixture =>
        {
            var first = fixture.Manager.Copilot.CaptureCopilotFlowSnapshot();
            Assert.Matches("^[0-9a-f]{32}$", first.SourceInstanceId);
            Assert.Equal(first.SourceInstanceId, fixture.Manager.Copilot.CaptureCopilotFlowSnapshot().SourceInstanceId);
            using (var other = new EditingFixture())
            {
                var otherSnapshot = other.Manager.Copilot.CaptureCopilotFlowSnapshot();
                Assert.Equal(first.Revision, otherSnapshot.Revision);
                Assert.NotEqual(first.SourceInstanceId, otherSnapshot.SourceInstanceId);
            }

            var node = new FlowRerouteNode();
            node.Create();
            fixture.Manager.View.STNodeEditorMain.Nodes.Add(node);
            var changed = fixture.Manager.Copilot.CaptureCopilotFlowSnapshot();
            Assert.NotEqual(first.Revision, changed.Revision);
            Assert.Equal(first.SourceInstanceId, changed.SourceInstanceId);
        });
    }

    [Fact]
    public void CopilotValidationUsesTheWholeRevisionAndPreservesCanvasSelectionHistoryLogAndContext()
    {
        WithFixture(fixture =>
        {
            var editor = fixture.Manager.View.STNodeEditorMain;
            for (var index = 0; index < 205; index++)
            {
                var node = new FlowRerouteNode();
                node.Create();
                editor.Nodes.Add(node);
            }
            var start = new TestStartNode();
            var end = new CVEndNode();
            start.Create();
            end.Create();
            editor.Nodes.Add(start);
            editor.Nodes.Add(end);
            Assert.Equal(ConnectionStatus.Connected, start.Output.ConnectOption(end.m_in_start));
            editor.ExecuteEditTransaction("Retain existing undo history", () => start.Left += 50);
            editor.SetActiveNode(start);
            editor.MoveCanvas(31, 47, false, CanvasMoveArgs.All);
            fixture.Manager.View.logTextBox.Text = "Existing diagnostic summary";
            string revision = fixture.Manager.Copilot.CaptureCopilotFlowSnapshot().Revision;

            var valid = fixture.ValidateCopilotWithoutEditing(revision.ToUpperInvariant());

            Assert.True(valid.RevisionMatches);
            Assert.True(valid.IsValid);
            Assert.Empty(valid.Diagnostic);
            Assert.Null(valid.InvalidNodeId);
            Assert.Equal(207, valid.NodeCount);
            Assert.Equal(1, valid.ConnectionCount);
            var instance = typeof(FlowEngineManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
            object? previous = instance.GetValue(null);
            instance.SetValue(null, fixture.Manager);
            try
            {
                var environment = new ColorVision.Copilot.Mcp.CopilotMcpToolEnvironment();
                var bound = environment.FlowGraphValidator(revision, CancellationToken.None).GetAwaiter().GetResult();
                Assert.Equal(valid, bound);
                var boundSnapshot = environment.FlowSnapshotProvider(CancellationToken.None).GetAwaiter().GetResult();
                Assert.NotNull(boundSnapshot);
                Assert.Equal(fixture.Manager.Copilot.CaptureCopilotFlowSnapshot().SourceInstanceId, boundSnapshot.SourceInstanceId);
                Assert.Equal(revision, boundSnapshot.Revision);
                using var cancellation = new CancellationTokenSource();
                cancellation.Cancel();
                Assert.ThrowsAny<OperationCanceledException>(() => environment.FlowSnapshotProvider(cancellation.Token).GetAwaiter().GetResult());
                instance.SetValue(null, null);
                Assert.Null(environment.FlowGraphValidator(revision, CancellationToken.None).GetAwaiter().GetResult());
                Assert.Null(environment.FlowSnapshotProvider(CancellationToken.None).GetAwaiter().GetResult());
                Assert.Null(instance.GetValue(null));
            }
            finally { instance.SetValue(null, previous); }

            start.Output.DisConnectOption(end.m_in_start);
            var stale = fixture.ValidateCopilotWithoutEditing(revision);
            Assert.False(stale.RevisionMatches);
            Assert.Null(stale.IsValid);
            Assert.Null(stale.InvalidNodeId);
            Assert.Empty(stale.Diagnostic);
            Assert.NotEqual(revision, stale.Revision);
            Assert.Equal(fixture.Manager.Copilot.CaptureCopilotFlowSnapshot().Revision, stale.Revision);
            var invalid = fixture.ValidateCopilotWithoutEditing(stale.Revision);
            Assert.True(invalid.RevisionMatches);
            Assert.False(invalid.IsValid);
            Assert.Equal(ColorVision.Engine.Properties.Resources.Flow_NoPathFromStartToEnd, invalid.Diagnostic);
            Assert.Equal(start.Guid, invalid.InvalidNodeId);
            foreach (string malformed in new[] { "", new string('0', 63), new string('z', 64) })
                Assert.Throws<ArgumentException>(() => fixture.Manager.Copilot.ValidateCopilotFlowGraph(malformed));
            Task.Run(() => Assert.Throws<InvalidOperationException>(() => fixture.Manager.Copilot.ValidateCopilotFlowGraph(stale.Revision))).GetAwaiter().GetResult();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CopilotValidationRemainsReadOnlyDuringPreparationPostProcessingAndStopping(bool postProcessing)
    {
        WithFixture(fixture =>
        {
            var template = new TemplateModel<FlowParam>("Validation phases", new FlowParam { Id = 709, Name = "Validation phases" });
            var (start, sink) = fixture.CreateMainGraph(template);
            var run = fixture.MainSession.RunFlowAndWaitForFinalizationAsync();
            fixture.Run = run;
            PumpUntil(Task.WhenAny(fixture.PreProcess.Entered.Task, run));
            Assert.True(fixture.PreProcess.Entered.Task.IsCompleted);
            if (postProcessing)
            {
                fixture.PreProcess.Release.SetResult(true);
                PumpUntil(Task.WhenAny(sink.Started.Task, run));
                Assert.True(sink.Started.Task.IsCompleted);
                start.Complete(sink.Started.Task.Result);
                PumpUntil(Task.WhenAny(fixture.PostProcess.Entered.Task, run));
                Assert.True(fixture.PostProcess.Entered.Task.IsCompleted);
            }
            string revision = fixture.Manager.Copilot.CaptureCopilotFlowSnapshot().Revision;
            var active = fixture.ValidateCopilotWithoutEditing(revision);
            Assert.True(active.RevisionMatches);
            Assert.NotNull(active.IsValid);
            Assert.True(active.IsExecutionActive);
            Assert.True(active.IsEditorReadOnly);
            Assert.False(fixture.Manager.FlowControl.IsFlowRun);
            fixture.MainSession.StopFlow(updateSummary: false);
            var stopping = fixture.ValidateCopilotWithoutEditing(revision);
            Assert.True(stopping.RevisionMatches);
            Assert.NotNull(stopping.IsValid);
            Assert.True(stopping.IsExecutionActive);
            Assert.True(stopping.IsEditorReadOnly);
            Assert.True(fixture.Manager.View.IsExecutionStopping);
            if (postProcessing) fixture.PostProcess.Release.SetResult([]);
            else fixture.PreProcess.Release.SetResult(true);
            PumpUntil(run);
            var finished = fixture.ValidateCopilotWithoutEditing(fixture.Manager.Copilot.CaptureCopilotFlowSnapshot().Revision);
            Assert.False(finished.IsExecutionActive);
            Assert.False(finished.IsEditorReadOnly);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RunLocksPreparationGraphAndPostProcessingThenRestoresOriginalEditing(bool originallyEditable)
    {
        WithFixture(fixture =>
        {
            fixture.View.STNodeEditorMain.EnableEdit = originallyEditable;
            AssertExecutionControls(fixture.View, active: false, stopping: false, indeterminate: true);
            Task<FlowRunFinalizedData?> run = fixture.Session.RunFlowAndWaitForFinalizationAsync();
            fixture.Run = run;

            // Editing must be locked before control returns to the caller, even before the engine starts.
            Assert.True(fixture.View.IsExecutionActive);
            fixture.AssertEditingBlocked();
            AssertExecutionControls(fixture.View, active: true, stopping: false, indeterminate: true);
            PumpUntil(Task.WhenAny(fixture.PreProcess.Entered.Task, run));
            Assert.True(fixture.PreProcess.Entered.Task.IsCompleted);
            fixture.AssertEditingBlocked();
            AssertExecutionControls(fixture.View, active: true, stopping: false, indeterminate: true);

            fixture.PreProcess.Release.SetResult(true);
            PumpUntil(Task.WhenAny(fixture.Sink.Started.Task, run));
            Assert.True(fixture.Sink.Started.Task.IsCompleted);
            Assert.True(fixture.View.FlowControl.IsFlowRun);
            fixture.AssertEditingBlocked();
            AssertExecutionControls(fixture.View, active: true, stopping: false, indeterminate: true);

            fixture.Start.Complete(fixture.Sink.Started.Task.Result);
            PumpUntil(Task.WhenAny(fixture.PostProcess.Entered.Task, run));
            Assert.True(fixture.PostProcess.Entered.Task.IsCompleted);
            Assert.False(fixture.View.FlowControl.IsFlowRun);
            fixture.AssertEditingBlocked();
            AssertExecutionControls(fixture.View, active: true, stopping: false, indeterminate: true);
            string completedSummary = fixture.View.logTextBox.Text;

            fixture.View.StopFlowCommand.Execute(null);
            Assert.False(run.IsCompleted);
            fixture.AssertEditingBlocked();
            AssertExecutionControls(fixture.View, active: true, stopping: true, indeterminate: true);
            Assert.Equal(completedSummary, fixture.View.logTextBox.Text);
            fixture.View.StopFlowCommand.Execute(null);
            Assert.Equal(completedSummary, fixture.View.logTextBox.Text);
            fixture.PostProcess.Release.SetResult([]);
            PumpUntil(run);

            Assert.Equal(FlowFinalOutcome.Succeeded, run.Result!.FinalOutcome);
            Assert.False(fixture.View.IsExecutionActive);
            Assert.Equal(originallyEditable, fixture.View.STNodeEditorMain.EnableEdit);
            Assert.Equal(originallyEditable, fixture.View.ClearCommand.CanExecute(null));
            AssertExecutionControls(fixture.View, active: false, stopping: false, indeterminate: true);
            Assert.Equal(completedSummary, fixture.View.logTextBox.Text);
            fixture.Session.StopFlow();
            fixture.View.StopFlowCommand.Execute(null);
            Assert.Equal(completedSummary, fixture.View.logTextBox.Text);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StoppingWhilePreProcessingWaitsKeepsGraphLockedUntilItReturns(bool originallyEditable)
    {
        WithFixture(fixture =>
        {
            fixture.View.STNodeEditorMain.EnableEdit = originallyEditable;
            Task<FlowRunFinalizedData?> run = fixture.Session.RunFlowAndWaitForFinalizationAsync();
            fixture.Run = run;
            PumpUntil(Task.WhenAny(fixture.PreProcess.Entered.Task, run));
            Assert.True(fixture.PreProcess.Entered.Task.IsCompleted);
            AssertExecutionControls(fixture.View, active: true, stopping: false, indeterminate: true);
            string preparingSummary = fixture.View.logTextBox.Text;

            fixture.View.StopFlowCommand.Execute(null);
            Assert.False(run.IsCompleted);
            fixture.AssertEditingBlocked();
            AssertExecutionControls(fixture.View, active: true, stopping: true, indeterminate: true);
            Assert.Equal(preparingSummary, fixture.View.logTextBox.Text);
            Assert.NotEqual(ColorVision.Engine.Properties.Resources.ExecutionCancelled, fixture.View.logTextBox.Text);
            fixture.PreProcess.Release.SetResult(true);
            PumpUntil(run);

            Assert.Null(run.Result);
            Assert.False(fixture.Sink.Started.Task.IsCompleted);
            Assert.False(fixture.PostProcess.Entered.Task.IsCompleted);
            Assert.False(fixture.View.IsExecutionActive);
            Assert.Equal(originallyEditable, fixture.View.STNodeEditorMain.EnableEdit);
            AssertExecutionControls(fixture.View, active: false, stopping: false, indeterminate: true);
            Assert.Equal(ColorVision.Engine.Properties.Resources.ExecutionCancelled, fixture.View.logTextBox.Text);
        });
    }

    [Fact]
    public void BackgroundEngineNotificationsRefreshBindingsFromCurrentStateAndIdleStopPreservesSummary()
    {
        WithFixture(fixture =>
        {
            ViewFlow view = fixture.Manager.View;
            view.ShowExecutionSummary("Previous run completed");
            AssertExecutionControls(view, active: false, stopping: false, indeterminate: true);

            Task.Run(() => fixture.Manager.FlowControl.IsFlowRun = true).GetAwaiter().GetResult();
            AssertExecutionControls(view, active: true, stopping: false, indeterminate: false);

            // Queue stale transitions without pumping; both notifications must present the final current state.
            Task.Run(() =>
            {
                fixture.Manager.FlowControl.IsFlowRun = false;
                fixture.Manager.FlowControl.IsFlowRun = true;
                fixture.Manager.FlowControl.IsFlowRun = false;
            }).GetAwaiter().GetResult();
            AssertExecutionControls(view, active: false, stopping: false, indeterminate: true);
            view.StopFlowCommand.Execute(null);
            Assert.Equal("Previous run completed", view.logTextBox.Text);
        });
    }

    [Fact]
    public void CopilotCannotMutateWhileSessionIsActiveWithoutRunningGraphOrEditorIsReadOnly()
    {
        WithFixture(fixture =>
        {
            ViewFlow view = fixture.Manager.View;
            var session = (FlowExecutionSession)typeof(ViewFlow).GetField("_executionSession", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view)!;
            var lifecycle = (FlowRunLifecycleGate)typeof(FlowExecutionSession).GetField("_runLifecycle", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
            using var cancellation = new CancellationTokenSource();
            Assert.True(lifecycle.TryBegin("SN-COPILOT-EDITING", cancellation, false));
            try
            {
                Assert.False(fixture.Manager.FlowControl.IsFlowRun);
                Assert.Contains("running", Assert.Throws<InvalidOperationException>(() =>
                    fixture.Manager.Copilot.AddCopilotFlowNode("must-not-be-created", 0, 0, null)).Message);
                session.StopFlow(updateSummary: false);
                Assert.Contains("running", Assert.Throws<InvalidOperationException>(() =>
                    fixture.Manager.Copilot.SetCopilotFlowNodeProperty("must-not-be-found", "Title", "Changed", null)).Message);
                Assert.Empty(view.STNodeEditorMain.Nodes.Cast<STNode>());
            }
            finally
            {
                lifecycle.Complete("SN-COPILOT-EDITING");
            }
            view.STNodeEditorMain.EnableEdit = false;
            Assert.Contains("read-only", Assert.Throws<InvalidOperationException>(() =>
                fixture.Manager.Copilot.AddCopilotFlowNode("must-not-be-created", 0, 0, null)).Message);
        });
    }

    [Fact]
    public void CopilotCameraAdditionKeepsConstructorDefaultsUnlessInheritanceIsRequested()
    {
        WithFixture(fixture =>
        {
            using var camera = new CurrentCameraDefaultsScope();
            var editor = fixture.Manager.View.STNodeEditorMain;
            editor.LoadAssembly(typeof(LocalCameraNode).Assembly);
            string typeKey = $"{typeof(LocalCameraNode).Module.Name}|{nameof(LocalCameraNode)}";
            string revision = fixture.Manager.Copilot.CaptureCopilotFlowSnapshot().Revision;

            var preview = fixture.Manager.Copilot.PreviewCopilotFlowNodeAddition(typeKey, 31, 47, revision);

            Assert.Null(preview.CameraDefaults);
            Assert.Null(preview.CameraDefaultsFingerprint);
            Assert.Empty(editor.Nodes.Cast<STNode>());
            var applied = fixture.Manager.Copilot.AddCopilotFlowNode(typeKey, 31, 47, revision);
            var node = Assert.Single(editor.Nodes.OfType<LocalCameraNode>());
            Assert.Equal(100f, node.ExpTime);
            Assert.Equal(0f, node.Gain);
            Assert.Equal(1, node.AvgCount);
            Assert.Equal(CVImageFlipMode.None, node.FlipMode);
            Assert.Empty(node.CalibTempName);
            Assert.All(applied.Nodes, item =>
            {
                Assert.Null(item.CameraDefaults);
                Assert.Null(item.CameraDefaultsFingerprint);
            });
        });
    }

    [Fact]
    public void CopilotCameraAdditionPreviewsEffectiveDefaultsAndAppliesOnlyTheReviewedValues()
    {
        WithFixture(fixture =>
        {
            using var camera = new CurrentCameraDefaultsScope();
            var editor = fixture.Manager.View.STNodeEditorMain;
            editor.LoadAssembly(typeof(LocalCameraNode).Assembly);
            string typeKey = $"{typeof(LocalCameraNode).Module.Name}|{nameof(LocalCameraNode)}";
            string revision = fixture.Manager.Copilot.CaptureCopilotFlowSnapshot().Revision;
            byte[] canvas = editor.GetCanvasData();
            var catalog = fixture.Manager.Copilot.CaptureCopilotFlowNodeCatalog(nameof(LocalCameraNode), 100);
            var cameraType = Assert.Single(catalog.NodeTypes.Where(item => item.TypeKey == typeKey));
            Assert.True(cameraType.CanInheritCameraDefaults);
            Assert.Empty(cameraType.AlgorithmHelpTopicId);
            var algorithmCatalog = fixture.Manager.Copilot.CaptureCopilotFlowNodeCatalog(nameof(LocalGridDistortionNode), 100);
            var algorithmType = Assert.Single(algorithmCatalog.NodeTypes);
            Assert.Equal(typeof(LocalGridDistortionNode).FullName, algorithmType.RuntimeType);
            Assert.Equal("algorithms.grid-distortion", algorithmType.AlgorithmHelpTopicId);

            var preview = fixture.Manager.Copilot.PreviewCopilotFlowNodeAddition(typeKey, 31, 47, revision, inheritCameraDefaults: true);
            var defaults = Assert.IsType<CopilotFlowCameraDefaults>(preview.CameraDefaults);
            Assert.Equal(camera.Camera.Code, defaults.DeviceCode);
            Assert.Equal(new[] { 800f }, defaults.ExposureMilliseconds);
            Assert.Equal(7f, defaults.Gain);
            Assert.Equal(4, defaults.AverageCount);
            Assert.Equal("XY", defaults.FlipMode);
            Assert.Equal("calibration-template", defaults.CalibrationTemplateName);
            Assert.False(string.IsNullOrWhiteSpace(preview.CameraDefaultsFingerprint));
            Assert.Equal(canvas, editor.GetCanvasData());
            Assert.Equal(revision, fixture.Manager.Copilot.CaptureCopilotFlowSnapshot().Revision);

            // These source values do not affect the single-exposure node or the calibration group's effective gain.
            camera.Display.ExpTimeR = 999;
            camera.Display.Gain = 99;
            var repeatedPreview = fixture.Manager.Copilot.PreviewCopilotFlowNodeAddition(typeKey, 90, 120, revision, inheritCameraDefaults: true);
            Assert.Equal(preview.CameraDefaultsFingerprint, repeatedPreview.CameraDefaultsFingerprint);
            fixture.Manager.Copilot.AddCopilotFlowNode(typeKey, 31, 47, revision,
                inheritCameraDefaults: true, cameraDefaultsFingerprint: preview.CameraDefaultsFingerprint);

            var node = Assert.Single(editor.Nodes.OfType<LocalCameraNode>());
            Assert.Equal(camera.Camera.Code, node.DeviceCode);
            Assert.Equal(800f, node.ExpTime);
            Assert.Equal(7f, node.Gain);
            Assert.Equal(4, node.AvgCount);
            Assert.Equal(CVImageFlipMode.XY, node.FlipMode);
            Assert.Equal("calibration-template", node.CalibTempName);
            Assert.Equal(31, node.Left);
            Assert.Equal(47, node.Top);
        });
    }

    [Fact]
    public void CopilotCameraAdditionRejectsCameraChangesDuringApprovalWithoutChangingTheCanvas()
    {
        WithFixture(fixture =>
        {
            using var camera = new CurrentCameraDefaultsScope();
            var editor = fixture.Manager.View.STNodeEditorMain;
            editor.LoadAssembly(typeof(LocalCameraNode).Assembly);
            string typeKey = $"{typeof(LocalCameraNode).Module.Name}|{nameof(LocalCameraNode)}";
            string revision = fixture.Manager.Copilot.CaptureCopilotFlowSnapshot().Revision;
            byte[] canvas = editor.GetCanvasData();
            var preview = fixture.Manager.Copilot.PreviewCopilotFlowNodeAddition(typeKey, 31, 47, revision, inheritCameraDefaults: true);
            camera.Display.ExpTime = 1600;

            Assert.Equal(revision, fixture.Manager.Copilot.CaptureCopilotFlowSnapshot().Revision);
            Assert.Contains("changed after preview", Assert.Throws<InvalidOperationException>(() =>
                fixture.Manager.Copilot.AddCopilotFlowNode(typeKey, 31, 47, revision,
                    inheritCameraDefaults: true, cameraDefaultsFingerprint: preview.CameraDefaultsFingerprint)).Message);
            Assert.Equal(canvas, editor.GetCanvasData());
            Assert.Empty(editor.Nodes.Cast<STNode>());
        });
    }

    [Fact]
    public void CopilotCameraAdditionRejectsMissingApprovalAndUnavailableOrInvalidDefaults()
    {
        WithFixture(fixture =>
        {
            using var camera = new CurrentCameraDefaultsScope();
            var editor = fixture.Manager.View.STNodeEditorMain;
            editor.LoadAssembly(typeof(LocalCameraNode).Assembly);
            string typeKey = $"{typeof(LocalCameraNode).Module.Name}|{nameof(LocalCameraNode)}";
            string revision = fixture.Manager.Copilot.CaptureCopilotFlowSnapshot().Revision;
            byte[] canvas = editor.GetCanvasData();

            Assert.Contains("provide their fingerprint", Assert.Throws<InvalidOperationException>(() =>
                fixture.Manager.Copilot.AddCopilotFlowNode(typeKey, 0, 0, revision, inheritCameraDefaults: true)).Message);
            Assert.Contains("requires inheritCameraDefaults=true", Assert.Throws<InvalidOperationException>(() =>
                fixture.Manager.Copilot.AddCopilotFlowNode(typeKey, 0, 0, revision, cameraDefaultsFingerprint: "unexpected")).Message);
            Assert.Contains("requires inheritCameraDefaults=true", Assert.Throws<InvalidOperationException>(() =>
                fixture.Manager.Copilot.PreviewCopilotFlowNodeAddition(typeKey, 0, 0, revision, cameraDefaultsFingerprint: "unexpected")).Message);
            string imageTypeKey = $"{typeof(LocalImageNode).Module.Name}|{nameof(LocalImageNode)}";
            Assert.Contains("does not support", Assert.Throws<InvalidOperationException>(() =>
                fixture.Manager.Copilot.PreviewCopilotFlowNodeAddition(imageTypeKey, 0, 0, revision, inheritCameraDefaults: true)).Message);
            camera.Display.ExpTime = double.NaN;
            Assert.Contains("valid acquisition defaults", Assert.Throws<InvalidOperationException>(() =>
                fixture.Manager.Copilot.PreviewCopilotFlowNodeAddition(typeKey, 0, 0, revision, inheritCameraDefaults: true)).Message);
            camera.Manager.DeviceServices.Clear();
            Assert.Contains("No available camera", Assert.Throws<InvalidOperationException>(() =>
                fixture.Manager.Copilot.PreviewCopilotFlowNodeAddition(typeKey, 0, 0, revision, inheritCameraDefaults: true)).Message);
            Assert.Equal(canvas, editor.GetCanvasData());
            Assert.Empty(editor.Nodes.Cast<STNode>());
        });
    }

    [Fact]
    public void CopilotPropertyEditingPreservesTimerStateWithoutReloadingTheNode()
    {
        WithFixture(fixture =>
        {
            var timer = (CVCommonNode)Activator.CreateInstance(typeof(CVCommonNode).Assembly.GetType("FlowEngineLib.simulator.Timer", true)!)!;
            timer.Create();
            timer.Mark = "Keep this timer annotation";
            timer.SetRepeatRegion("copilot-timer-repeat", 3, 1);
            timer.SetRepeatParameterValues(new Dictionary<string, double[]> { ["Time"] = [100, 200, 300] });
            var time = timer.GetType().GetProperty("Time")!;
            time.SetValue(timer, 100);
            var editor = fixture.Manager.View.STNodeEditorMain;
            editor.Nodes.Add(timer);
            byte[] originalCanvas = editor.GetCanvasData();
            var originalRevision = fixture.Manager.Copilot.CaptureCopilotFlowSnapshot().Revision;
            var inputCount = timer.GetAllInputOptions().Length;
            var outputCount = timer.GetAllOutputOptions().Length;

            var preview = fixture.Manager.Copilot.PreviewCopilotFlowNodePropertyChange(timer.NodeID, "Time", "250", originalRevision);

            Assert.Equal("100", preview.OldValue);
            Assert.Equal("250", preview.NewValue);
            Assert.Equal(timer.NodeID, preview.Node.InstanceId);
            Assert.Equal(timer.Mark, preview.Node.Mark);
            Assert.Equal(originalCanvas, editor.GetCanvasData());
            Assert.Equal(100, time.GetValue(timer));

            fixture.Manager.Copilot.SetCopilotFlowNodeProperty(timer.NodeID, "Time", "250", originalRevision);

            Assert.Equal(250, time.GetValue(timer));
            Assert.Equal("Keep this timer annotation", timer.Mark);
            Assert.Equal("copilot-timer-repeat", timer.RepeatRegionId);
            Assert.Equal(3, timer.RepeatCount);
            Assert.Equal(1, timer.RepeatMemberCount);
            Assert.Equal(new double[] { 100, 200, 300 }, timer.RepeatParameterValues["Time"]);
            Assert.Equal(inputCount, timer.GetAllInputOptions().Length);
            Assert.Equal(outputCount, timer.GetAllOutputOptions().Length);
        });
    }

    [Fact]
    public void CopilotPropertyEditingUsesTheTextDescriptorAndRollsBackAFailedSetter()
    {
        WithFixture(fixture =>
        {
            var node = new DescriptorEditingNode();
            node.Create();
            node.Value = 7;
            node.Mark = "Preserve custom node annotation";
            var editor = fixture.Manager.View.STNodeEditorMain;
            editor.Nodes.Add(node);
            var revision = fixture.Manager.Copilot.CaptureCopilotFlowSnapshot().Revision;

            var preview = fixture.Manager.Copilot.PreviewCopilotFlowNodePropertyChange(node.NodeID, nameof(node.Value), "units:42", revision);
            Assert.Equal("7", preview.OldValue);
            Assert.Equal("42", preview.NewValue);
            Assert.Equal(7, node.Value);
            fixture.Manager.Copilot.SetCopilotFlowNodeProperty(node.NodeID, nameof(node.Value), "units:42", revision);
            Assert.Equal(42, node.Value);
            byte[] appliedCanvas = editor.GetCanvasData();

            var exception = Assert.Throws<InvalidOperationException>(() =>
                fixture.Manager.Copilot.SetCopilotFlowNodeProperty(node.NodeID, nameof(node.Value), "units:13", null));

            Assert.Contains("The test setter rejected its new value", exception.Message, StringComparison.Ordinal);
            Assert.Equal(42, node.Value);
            Assert.Equal("Preserve custom node annotation", node.Mark);
            Assert.Equal(appliedCanvas, editor.GetCanvasData());
        });
    }

    [Fact]
    public void ReconcilingUnchangedTemplateDuringPreProcessingKeepsRunActiveAndEditingLocked()
    {
        WithFixture(fixture =>
        {
            var template = new TemplateModel<FlowParam>("Running flow", new FlowParam { Id = 704, Name = "Running flow" });
            TemplateFlow.Params.Add(template);
            var (_, sink) = fixture.CreateMainGraph(template);
            ViewFlow view = fixture.Manager.View;
            template.Value.DataBase64 = Convert.ToBase64String(view.STNodeEditorMain.GetCanvasData());
            view.SetDocumentBaseline(template.Value);
            Task<FlowRunFinalizedData?> run = fixture.MainSession.RunFlowAndWaitForFinalizationAsync();
            fixture.Run = run;
            PumpUntil(Task.WhenAny(fixture.PreProcess.Entered.Task, run));
            Assert.True(fixture.PreProcess.Entered.Task.IsCompleted);
            Assert.False(run.IsCompleted);
            Assert.False(fixture.MainSession.IsStopRequested);
            Assert.False(view.STNodeEditorMain.EnableEdit);
            AssertExecutionControls(view, active: true, stopping: false, indeterminate: true);
            string summary = view.logTextBox.Text;
            STNode[] nodes = view.STNodeEditorMain.Nodes.Cast<STNode>().ToArray();
            var latest = new FlowParam
            {
                Id = template.Id,
                Name = "Renamed running flow",
                DataBase64 = template.Value.DataBase64,
                TemplateRevision = 9,
            };
            template.Value = latest;

            Task reconcile = view.ReconcileTemplatesAsync();

            Assert.True(reconcile.IsCompleted);
            reconcile.GetAwaiter().GetResult();
            Assert.False(run.IsCompleted);
            Assert.False(fixture.MainSession.IsStopRequested);
            Assert.False(view.STNodeEditorMain.EnableEdit);
            Assert.False(view.ClearCommand.CanExecute(null));
            AssertExecutionControls(view, active: true, stopping: false, indeterminate: true);
            Assert.Equal(summary, view.logTextBox.Text);
            Assert.Equal(nodes, view.STNodeEditorMain.Nodes.Cast<STNode>());
            Assert.Same(latest, fixture.Manager.SelectedFlowParam);
            Assert.False(sink.Started.Task.IsCompleted);
            Assert.False(fixture.PostProcess.Entered.Task.IsCompleted);

            fixture.MainSession.StopFlow(updateSummary: false);
            fixture.PreProcess.Release.SetResult(true);
            PumpUntil(run);
            Assert.Null(run.Result);
            Assert.False(view.IsExecutionActive);
            Assert.True(view.STNodeEditorMain.EnableEdit);
            Assert.False(sink.Started.Task.IsCompleted);
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ExplicitTemplateRunCannotReplaceActivePreOrPostProcessing(bool postProcessing, bool waitForFinalization)
    {
        WithFixture(fixture =>
        {
            var templateA = new TemplateModel<FlowParam>("Active flow", new FlowParam { Id = 705, Name = "Active flow" });
            var templateB = new TemplateModel<FlowParam>("Other flow", new FlowParam { Id = 706, Name = "Other flow" });
            var (start, sink) = fixture.CreateMainGraph(templateA);
            ViewFlow view = fixture.Manager.View;
            templateA.Value.DataBase64 = Convert.ToBase64String(view.STNodeEditorMain.GetCanvasData());
            view.SetDocumentBaseline(templateA.Value);
            Task<FlowRunFinalizedData?> activeRun = fixture.MainSession.RunFlowAndWaitForFinalizationAsync();
            fixture.Run = activeRun;
            PumpUntil(Task.WhenAny(fixture.PreProcess.Entered.Task, activeRun));
            Assert.True(fixture.PreProcess.Entered.Task.IsCompleted);
            if (postProcessing)
            {
                fixture.PreProcess.Release.SetResult(true);
                PumpUntil(Task.WhenAny(sink.Started.Task, activeRun));
                Assert.True(sink.Started.Task.IsCompleted);
                start.Complete(sink.Started.Task.Result);
                PumpUntil(Task.WhenAny(fixture.PostProcess.Entered.Task, activeRun));
                Assert.True(fixture.PostProcess.Entered.Task.IsCompleted);
            }
            string summary = view.logTextBox.Text;
            STNode[] nodes = view.STNodeEditorMain.Nodes.Cast<STNode>().ToArray();
            byte[] canvas = view.STNodeEditorMain.GetCanvasData();
            Assert.False(activeRun.IsCompleted);
            Assert.False(fixture.MainSession.IsStopRequested);

            if (waitForFinalization)
            {
                Task<FlowRunFinalizedData?> duplicate = fixture.Manager.RunFlowAndWaitForFinalizationAsync(templateB);
                Assert.True(duplicate.IsCompleted);
                Assert.Null(duplicate.GetAwaiter().GetResult());
            }
            else
            {
                Task<FlowControlData?> duplicate = fixture.Manager.RunFlowAsync(templateB);
                Assert.True(duplicate.IsCompleted);
                Assert.Null(duplicate.GetAwaiter().GetResult());
            }

            Assert.False(activeRun.IsCompleted);
            Assert.False(fixture.MainSession.IsStopRequested);
            Assert.Same(templateA.Value, fixture.Manager.SelectedFlowParam);
            Assert.Equal(templateA.Id, fixture.MainSession.RequestedTemplateId);
            Assert.Equal(nodes, view.STNodeEditorMain.Nodes.Cast<STNode>());
            Assert.Equal(canvas, view.STNodeEditorMain.GetCanvasData());
            Assert.Equal(summary, view.logTextBox.Text);
            Assert.False(view.STNodeEditorMain.EnableEdit);
            AssertExecutionControls(view, active: true, stopping: false, indeterminate: true);

            if (postProcessing)
            {
                fixture.PostProcess.Release.SetResult([]);
                PumpUntil(activeRun);
                Assert.Equal(FlowFinalOutcome.Succeeded, activeRun.Result!.FinalOutcome);
            }
            else
            {
                fixture.MainSession.StopFlow(updateSummary: false);
                fixture.PreProcess.Release.SetResult(true);
                PumpUntil(activeRun);
                Assert.Null(activeRun.Result);
                Assert.False(sink.Started.Task.IsCompleted);
            }
            Assert.True(view.STNodeEditorMain.EnableEdit);
        });
    }

    [Fact]
    public void TemplateSelectionReservesAllRunEntrypointsAndExpiredSelectionReleasesReservation()
    {
        WithFixture(fixture =>
        {
            var templateA = new TemplateModel<FlowParam>("Current flow", new FlowParam { Id = 707, Name = "Current flow" });
            var templateB = new TemplateModel<FlowParam>("Pending flow", new FlowParam { Id = 708, Name = "Pending flow" });
            var templateC = new TemplateModel<FlowParam>("Competing flow", new FlowParam { Id = 709, Name = "Competing flow" });
            var (_, sink) = fixture.CreateMainGraph(templateA);
            ViewFlow view = fixture.Manager.View;
            templateA.Value.DataBase64 = Convert.ToBase64String(view.STNodeEditorMain.GetCanvasData());
            view.SetDocumentBaseline(templateA.Value);
            var workspaceField = typeof(FlowExecutionSession).GetField("_templateWorkspace", BindingFlags.Instance | BindingFlags.NonPublic)!;
            ((FlowTemplateWorkspaceController)workspaceField.GetValue(fixture.MainSession)!).Dispose();
            var releaseSelection = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int closeCalls = 0;
            var workspace = new FlowTemplateWorkspaceController(fixture.Manager, view,
                () => { closeCalls++; return releaseSelection.Task; },
                () => { }, () => { }, _ => { }, _ => { }, () => false);
            workspaceField.SetValue(fixture.MainSession, workspace);
            workspace.AdoptSavedCanvas(templateA);
            var refreshGate = (FlowTemplateRefreshGate)typeof(FlowTemplateWorkspaceController)
                .GetField("_refreshGate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(workspace)!;
            view.ShowExecutionSummary("Previous result");
            STNode[] nodes = view.STNodeEditorMain.Nodes.Cast<STNode>().ToArray();
            Task<FlowControlData?> first = fixture.Manager.RunFlowAsync(templateB);
            fixture.Run = first;
            try
            {
                Assert.Equal(1, closeCalls);
                Assert.False(first.IsCompleted);
                Assert.False(fixture.MainSession.IsRunActive);
                Assert.False(fixture.MainSession.IsStopRequested);
                long generation = refreshGate.Generation;

                Task<FlowRunFinalizedData?> competing = fixture.Manager.RunFlowAndWaitForFinalizationAsync(templateC);
                Assert.True(competing.IsCompleted);
                Assert.Null(competing.GetAwaiter().GetResult());
                Task<FlowControlData?> current = fixture.MainSession.RunFlowAndWaitAsync();
                Assert.True(current.IsCompleted);
                Assert.Null(current.GetAwaiter().GetResult());
                Task ordinaryUiRun = fixture.MainSession.RunFlowAsync();
                Assert.True(ordinaryUiRun.IsCompleted);
                ordinaryUiRun.GetAwaiter().GetResult();
                view.RunFlowCommand.Execute(null);

                Assert.Equal(generation, refreshGate.Generation);
                Assert.Equal(templateB.Id, workspace.RequestedTemplateId);
                Assert.Equal(1, closeCalls);
                Assert.Equal(nodes, view.STNodeEditorMain.Nodes.Cast<STNode>());
                Assert.Equal("Previous result", view.logTextBox.Text);
                Assert.False(first.IsCompleted);
                Assert.False(fixture.PreProcess.Entered.Task.IsCompleted);
                Assert.False(fixture.MainSession.IsStopRequested);
            }
            finally
            {
                // Invalidate the pending selection without ever loading its synthetic template.
                workspace.AdoptSavedCanvas(templateA);
                releaseSelection.TrySetResult();
                PumpUntil(first);
            }
            Assert.Null(first.Result);

            Task<FlowRunFinalizedData?> retry = fixture.MainSession.RunFlowAndWaitForFinalizationAsync();
            fixture.Run = retry;
            PumpUntil(Task.WhenAny(fixture.PreProcess.Entered.Task, retry));
            Assert.True(fixture.PreProcess.Entered.Task.IsCompleted);
            Assert.False(retry.IsCompleted);
            Assert.False(fixture.MainSession.IsStopRequested);
            Assert.False(view.STNodeEditorMain.EnableEdit);
            fixture.MainSession.StopFlow(updateSummary: false);
            fixture.PreProcess.Release.SetResult(true);
            PumpUntil(retry);
            Assert.Null(retry.Result);
            Assert.False(sink.Started.Task.IsCompleted);
            Assert.True(view.STNodeEditorMain.EnableEdit);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitTemplateRunRejectsDirtyWorkspaceWithoutRunningOrReplacingIt(bool waitForFinalization)
    {
        WithFixture(fixture =>
        {
            var templateA = new TemplateModel<FlowParam>("A", new FlowParam { Id = 701, Name = "A" });
            var templateB = new TemplateModel<FlowParam>("B", new FlowParam { Id = 702, Name = "B" });
            var (start, sink) = fixture.CreateMainGraph(templateA);
            STNodeEditor editor = fixture.Manager.View.STNodeEditorMain;
            editor.ExecuteEditTransaction("Move unsaved node", () => start.Left += 80);
            Assert.True(editor.IsModified);
            Assert.True(editor.CanUndo);
            STNode[] nodes = editor.Nodes.Cast<STNode>().ToArray();
            byte[] canvas = editor.GetCanvasData();

            if (waitForFinalization)
            {
                Task<FlowRunFinalizedData?> run = fixture.Manager.RunFlowAndWaitForFinalizationAsync(templateB);
                fixture.Run = run;
                Assert.True(run.IsCompleted);
                Assert.Null(run.GetAwaiter().GetResult());
            }
            else
            {
                Task<FlowControlData?> run = fixture.Manager.RunFlowAsync(templateB);
                fixture.Run = run;
                Assert.True(run.IsCompleted);
                Assert.Null(run.GetAwaiter().GetResult());
            }

            Assert.Equal(nodes, editor.Nodes.Cast<STNode>().ToArray());
            Assert.Equal(canvas, editor.GetCanvasData());
            Assert.True(editor.IsModified);
            Assert.True(editor.CanUndo);
            Assert.Same(templateA.Value, fixture.Manager.SelectedFlowParam);
            Assert.False(fixture.Manager.View.IsExecutionActive);
            Assert.False(fixture.PreProcess.Entered.Task.IsCompleted);
            Assert.False(sink.Started.Task.IsCompleted);
            Assert.False(fixture.PostProcess.Entered.Task.IsCompleted);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SupersededExecutionSnapshotCannotRunReplacementCanvasEvenForSameTemplate(bool waitForFinalization)
    {
        WithFixture(fixture =>
        {
            var template = new TemplateModel<FlowParam>("Selected flow", new FlowParam { Id = 703, Name = "Selected flow" });
            fixture.CreateMainGraph(template);
            var workspace = (FlowTemplateWorkspaceController)typeof(FlowExecutionSession)
                .GetField("_templateWorkspace", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.MainSession)!;
            Task<FlowTemplateExecutionSnapshotResult> selected = workspace.WaitForExecutionSnapshotAsync();
            Assert.True(selected.IsCompleted);
            FlowTemplateExecutionSnapshot snapshot = Assert.IsType<FlowTemplateExecutionSnapshot>(selected.Result.Snapshot);

            // Re-adopting a replacement canvas invalidates the generation even when the template ID is unchanged.
            var (_, replacementSink) = fixture.CreateMainGraph(template);
            STNodeEditor editor = fixture.Manager.View.STNodeEditorMain;
            STNode[] nodes = editor.Nodes.Cast<STNode>().ToArray();
            Assert.False(workspace.IsCurrentExecutionSnapshot(snapshot));
            if (waitForFinalization)
            {
                Task<FlowRunFinalizedData?> run = fixture.MainSession.RunFlowAndWaitForFinalizationAsync(snapshot);
                fixture.Run = run;
                Assert.True(run.IsCompleted);
                Assert.Null(run.GetAwaiter().GetResult());
            }
            else
            {
                Task<FlowControlData?> run = fixture.MainSession.RunFlowAndWaitAsync(snapshot);
                fixture.Run = run;
                Assert.True(run.IsCompleted);
                Assert.Null(run.GetAwaiter().GetResult());
            }

            Assert.Equal(nodes, editor.Nodes.Cast<STNode>().ToArray());
            Assert.False(editor.IsModified);
            Assert.True(editor.EnableEdit);
            Assert.False(fixture.Manager.View.IsExecutionActive);
            Assert.False(fixture.PreProcess.Entered.Task.IsCompleted);
            Assert.False(replacementSink.Started.Task.IsCompleted);
            Assert.False(fixture.PostProcess.Entered.Task.IsCompleted);
            Assert.Equal(EngineLocalization.Get("流程模板正在切换，请稍后重试。"), fixture.Manager.View.logTextBox.Text);
        });
    }

    internal static void WithCopilotGraph(Action<FlowCopilotService, STNodeEditor> action)
        => WithFixture(fixture => action(fixture.Manager.Copilot, fixture.Manager.View.STNodeEditorMain));

    private static void WithFixture(Action<EditingFixture> action)
    {
        WpfTestHost.Invoke(() =>
        {
            var previousConfig = ConfigService.Instance;
            var previousTemplates = TemplateFlow.Params;
            ConfigService.SetInstance(new ConfigHandler());
            TemplateFlow.Params = [];
            var theme = new ResourceDictionary { Source = new Uri("/ColorVision.Themes;component/Themes/Theme.xaml", UriKind.Relative) };
            Application.Current.Resources.MergedDictionaries.Add(theme);
            try
            {
                Assert.False(MySqlSetting.IsConnect, "This fixture must not run against a connected result database.");
                using var fixture = new EditingFixture();
                action(fixture);
            }
            finally
            {
                Application.Current.Resources.MergedDictionaries.Remove(theme);
                ConfigService.SetInstance(previousConfig);
                TemplateFlow.Params = previousTemplates;
            }
        });
    }

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

    private static void AssertExecutionControls(ViewFlow view, bool active, bool stopping, bool indeterminate)
    {
        // Drain actual WPF binding and command requery work; no manual binding refresh or timing assumptions.
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);

        Assert.Equal(active, view.IsExecutionActive);
        Assert.Equal(stopping, view.IsExecutionStopping);
        Assert.Equal(indeterminate, view.IsExecutionProgressIndeterminate);
        Assert.Equal(active ? Visibility.Collapsed : Visibility.Visible, view.RunButton.Visibility);
        Assert.Equal(!active, view.RunButton.IsEnabled);
        Assert.Equal(active ? Visibility.Visible : Visibility.Collapsed, view.StopButton.Visibility);
        Assert.Equal(active && !stopping, view.StopButton.IsEnabled);
        Assert.Equal(stopping ? Visibility.Visible : Visibility.Collapsed, view.StoppingText.Visibility);
        Assert.Equal(active ? Visibility.Visible : Visibility.Collapsed, view.ProgressBar1.Visibility);
        Assert.Equal(indeterminate, view.ProgressBar1.IsIndeterminate);
        Assert.False(string.IsNullOrWhiteSpace(view.StoppingText.Text));
    }

    private sealed class EditingFixture : IDisposable
    {
        private static readonly FieldInfo PreProcessInstance = typeof(PreProcessManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? previousPreProcess = PreProcessInstance.GetValue(null);
        public FlowEngineManager Manager { get; } = new();
        public ViewFlow View { get; }
        public FlowExecutionSession Session { get; }
        public FlowExecutionSession MainSession { get; }
        public ControlledPreProcess PreProcess { get; } = new();
        public ControlledPostProcess PostProcess { get; } = new();
        public TestStartNode Start { get; } = new();
        public StartSinkNode Sink { get; } = new();
        public Task? Run { get; set; }

        public EditingFixture()
        {
            // Supply only the action list read by ExecuteAsync; never load or persist the user's processor configuration.
            var preProcessManager = (PreProcessManager)RuntimeHelpers.GetUninitializedObject(typeof(PreProcessManager));
            typeof(PreProcessManager).GetField("<Processes>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(preProcessManager, new ObservableCollection<PreProcessAction> { new(PreProcess) { IsEnabled = true } });
            PreProcessInstance.SetValue(null, preProcessManager);
            View = new ViewFlow(Manager, isStandalone: true);
            View.FlowEngineControl.AttachNodeEditor(View.STNodeEditorMain);
            Session = (FlowExecutionSession)typeof(ViewFlow).GetField("_executionSession", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(View)!;
            MainSession = (FlowExecutionSession)typeof(ViewFlow).GetField("_executionSession", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Manager.View)!;
            typeof(FlowExecutionSession).GetField("_runFinalizer", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(Session, new FlowRunFinalizer(PostProcess, new NoPersistence()));
            typeof(FlowExecutionSession).GetField("_runFinalizer", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(MainSession, new FlowRunFinalizer(PostProcess, new NoPersistence()));
            Start.Create();
            Sink.Create();
            View.STNodeEditorMain.Nodes.Add(Start);
            View.STNodeEditorMain.Nodes.Add(Sink);
            Assert.Equal(ConnectionStatus.Connected, Start.Output.ConnectOption(Sink.Input));
        }

        public (TestStartNode Start, StartSinkNode Sink) CreateMainGraph(TemplateModel<FlowParam> template)
        {
            STNodeEditor editor = Manager.View.STNodeEditorMain;
            editor.Nodes.Clear();
            var start = new TestStartNode();
            var sink = new StartSinkNode();
            start.Create();
            sink.Create();
            editor.Nodes.Add(start);
            editor.Nodes.Add(sink);
            Assert.Equal(ConnectionStatus.Connected, start.Output.ConnectOption(sink.Input));
            MainSession.AdoptSavedCanvas(template);
            editor.MarkSaved();
            return (start, sink);
        }

        public CopilotFlowValidationSnapshot ValidateCopilotWithoutEditing(string revision)
        {
            var view = Manager.View;
            var editor = view.STNodeEditorMain;
            var nodes = editor.Nodes.Cast<STNode>().ToArray();
            byte[] canvas = editor.GetCanvasData();
            var active = editor.ActiveNode;
            var selected = nodes.Where(node => node.IsSelected).ToArray();
            var history = (editor.IsModified, editor.CanUndo, editor.CanRedo);
            string summary = view.logTextBox.Text;
            var context = CopilotLiveContextRegistry.Current;
            int publications = 0;
            void OnContextChanged(object? _, EventArgs __) => publications++;
            CopilotLiveContextRegistry.CurrentChanged += OnContextChanged;
            CopilotFlowValidationSnapshot result;
            try { result = Manager.Copilot.ValidateCopilotFlowGraph(revision); }
            finally { CopilotLiveContextRegistry.CurrentChanged -= OnContextChanged; }
            Assert.Equal(nodes, editor.Nodes.Cast<STNode>().ToArray());
            Assert.Equal(canvas, editor.GetCanvasData());
            Assert.Same(active, editor.ActiveNode);
            Assert.Equal(selected, editor.Nodes.Cast<STNode>().Where(node => node.IsSelected).ToArray());
            Assert.Equal(history, (editor.IsModified, editor.CanUndo, editor.CanRedo));
            Assert.Equal(summary, view.logTextBox.Text);
            Assert.Same(context, CopilotLiveContextRegistry.Current);
            Assert.Equal(0, publications);
            Assert.Equal(nodes.Length, result.NodeCount);
            Assert.Equal(editor.GetConnectionInfo().Length, result.ConnectionCount);
            Assert.Equal(view.IsExecutionActive, result.IsExecutionActive);
            Assert.Equal(!editor.EnableEdit, result.IsEditorReadOnly);
            return result;
        }

        public void AssertEditingBlocked()
        {
            STNodeEditor editor = View.STNodeEditorMain;
            Assert.False(editor.EnableEdit);
            Assert.True(editor.IsEnabled);
            Assert.True(View.EditorCanvas.IsEnabled);
            Assert.False(View.ClearCommand.CanExecute(null));
            Assert.False(View.NewFlowCommand.CanExecute(null));
            Assert.False(View.ImportFlowCommand.CanExecute(null));
            Assert.False(View.ImportModuleCommand.CanExecute(null));
            Assert.False(View.AutoAlignmentCommand.CanExecute(null));
            Assert.False(editor.CanUndo);
            Assert.False(editor.CanRedo);

            STNode[] nodes = editor.Nodes.Cast<STNode>().ToArray();
            View.Clear();
            View.NewFlowCommand.Execute(null);
            View.ImportFlow();
            View.ImportModule();
            View.AutoAlignment();
            View.DeleteFlow();
            editor.Undo();
            editor.Redo();
            Assert.Throws<InvalidOperationException>(() => View.LoadStandaloneFlowParam(new FlowParam(), false));
            Assert.Throws<InvalidOperationException>(() => View.LoadStandaloneFile("must-not-be-read.stn"));
            Assert.Equal(nodes, editor.Nodes.Cast<STNode>().ToArray());
            Assert.Equal(1, Start.Output.ConnectionCount);

            editor.SetActiveNode(Start);
            View.EditorCanvas.RefreshNodePropertyPanel();
            Assert.False(View.EditorCanvas.NodePropertyPanel.IsEnabled);
            Assert.True(View.EditorCanvas.DocumentationViewButton.IsEnabled);
            View.EditorCanvas.DocumentationViewButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(View.EditorCanvas.NodePropertyPanel.IsEnabled);
            View.EditorCanvas.ConfigurationViewButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.False(View.EditorCanvas.NodePropertyPanel.IsEnabled);
            editor.MoveCanvas(42, 35, false, CanvasMoveArgs.All);
            Assert.Equal(42, editor.CanvasOffsetX);
            Assert.Equal(35, editor.CanvasOffsetY);
        }

        public void Dispose()
        {
            Session.StopFlow(updateSummary: false);
            MainSession.StopFlow(updateSummary: false);
            PreProcess.Release.TrySetResult(false);
            PostProcess.Release.TrySetResult([]);
            if (Run != null) PumpUntil(Run);
            View.Dispose();
            Manager.View.Dispose();
            Manager.DisplayFlow.Dispose();
            Manager.FlowEngineControl.Dispose();
            DockViewManager.GetInstance().RemoveView(Manager.View);
            (Manager.Copilot.GetType().GetField("_copilotAgentExtensionRegistration", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Manager.Copilot) as IDisposable)?.Dispose();
            PreProcessInstance.SetValue(null, previousPreProcess);
        }
    }

    private sealed class ControlledPreProcess : IPreProcessor
    {
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<bool> PreProcess(PreProcessContext context)
        {
            Entered.TrySetResult(true);
            return Release.Task;
        }
    }

    private sealed class ControlledPostProcess : IFlowPostProcessExecutor
    {
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<IReadOnlyList<PostProcessExecutionResult>> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IReadOnlyList<PostProcessExecutionResult>> ExecuteAsync(MeasureBatchModel batch, string flowName)
        {
            Entered.TrySetResult(true);
            return Release.Task;
        }
    }

    private sealed class NoPersistence : IFlowRunFinalizationPersistence
    {
        public void ApplyRequiredPostProcessFailure(MeasureBatchModel batch, IReadOnlyList<PostProcessExecutionResult> results) { }
        public void RecordFallbackRun(MeasureBatchModel batch, string flowName, FlowControlData result, FlowStatus status, long elapsedMilliseconds) { }
    }

    public sealed class DescriptorEditingNode : CVCommonNode
    {
        private int _value;
        public DescriptorEditingNode() : base("Descriptor editing test", "Descriptor test", "Descriptor test") { }

        [STNodeProperty("Value", "A value with distinct text and persistence encodings", DescriptorType = typeof(UnitsPropertyDescriptor))]
        public int Value
        {
            get => _value;
            set
            {
                _value = value;
                if (value == 13) throw new InvalidOperationException("The test setter rejected its new value");
                OnPropertyChanged();
            }
        }
    }

    public sealed class UnitsPropertyDescriptor : STNodePropertyDescriptor
    {
        protected override object GetValueFromString(string text) => text.StartsWith("units:", StringComparison.Ordinal)
            ? int.Parse(text[6..], CultureInfo.InvariantCulture) : throw new FormatException("Expected units:<integer>");
        protected override byte[] GetBytesFromValue() => BitConverter.GetBytes((int)PropertyInfo.GetValue(Node)!);
        protected override object GetValueFromBytes(byte[] data) => BitConverter.ToInt32(data, 0);
    }

    private sealed class TestStartNode : BaseStartNode
    {
        public TestStartNode() : base("Editing test start") => NodeName = "Editing test start";
        public STNodeOption Output => m_op_start;
        public void Complete(CVStartCFC action)
        {
            action.SetStatusType(StatusTypeEnum.Completed);
            action.DoFinishing();
            action.FireFinished();
        }
    }

    private sealed class StartSinkNode : STNode
    {
        public STNodeOption Input { get; private set; } = null!;
        public TaskCompletionSource<CVStartCFC> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override void OnCreate()
        {
            base.OnCreate();
            Input = InputOptions.Add("IN", typeof(CVStartCFC), true);
            Input.DataTransfer += (_, e) =>
            {
                if (e.TargetOption.Data is CVStartCFC action && !action.IsStop) Started.TrySetResult(action);
            };
        }
    }
}
