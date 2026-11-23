using ColorVision.Database;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Flow.Versioning;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.UI;
using ColorVision.UI.Views;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class FlowTemplateWorkspaceEditingTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void WorkspaceDeletesOnlyTheConfirmedFlowWhenOtherTemplatesAreMarked(int markedCount)
    {
        string path = Path.Combine(Path.GetTempPath(), "ColorVision-workspace-delete-tests", Guid.NewGuid().ToString("N"), "local.db");
        var storage = new LocalFlowTemplateStorage(new LocalTemplateStore(path));
        WithFixture(fixture =>
        {
            foreach (var template in new[] { fixture.A, fixture.B, fixture.C })
                storage.Save(new FlowParam { Name = template.Key, DataBase64 = template.Value.DataBase64 });

            fixture.Manager.CreateFlowTemplate().Load();
            TemplateModel<FlowParam> confirmed = TemplateFlow.Params.Single(template => template.Key == "A");
            var load = fixture.View.SelectFlowTemplateAsync(confirmed, replacementMode: FlowWorkspaceReplacementMode.AlreadyConfirmed);
            PumpUntil(load);
            Assert.NotNull(load.Result.Snapshot);
            var marked = TemplateFlow.Params.Where(template => template != confirmed).Take(markedCount).ToArray();
            foreach (var template in marked) template.IsSelected = true;

            PumpUntil(fixture.View.DeleteFlowAsync(name =>
            {
                Assert.Equal("A", name);
                return true;
            }));

            Assert.Equal(new[] { "B", "C" }, storage.Load().Select(flow => flow.Name));
            Assert.All(marked, template => Assert.True(template.IsSelected));
        }, associateInitialTemplate: false, createFlowTemplate: () => new TemplateFlow(storage, () => false,
            () => throw new InvalidOperationException("This workflow test must not open MySQL.")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HistoricalRepeatRestoreReplacesCanvasWithoutSavingDraftAgain(bool dirty)
    {
        WithFixture(fixture =>
        {
            using var historical = new STNodeEditor();
            var node = new LocalSmuNode();
            node.Create();
            historical.Nodes.Add(node);
            node.SetRepeatRegion("restored-repeat", 3, 1);
            node.SetRepeatParameterValues(new Dictionary<string, double[]>
            {
                [nameof(LocalSmuNode.MeasureValue)] = [-1.2, -1.3, -1.4],
            });
            fixture.A.Value.FlowKey = "flow:restored-repeat";
            if (dirty) fixture.MakeDirty();
            var revision = new FlowRevision
            {
                FlowKey = fixture.A.Value.FlowKey,
                Revision = 5,
                FullSnapshot = historical.GetCanvasData(),
            };
            var restore = new FlowVersionRestoreService((param, _) =>
            {
                param.LoadedContentHash = FlowSemanticHash.ComputeBinaryHash(revision.FullSnapshot);
                param.TemplateRevision = 6;
            });
            Assert.True(restore.Restore(new FlowVersionRestoreRequest(fixture.A.Value, revision, null)).Succeeded);

            PumpUntil(fixture.View.ReloadRestoredVersionAsync(fixture.A.Value));

            LocalSmuNode loaded = Assert.Single(fixture.Editor.Nodes.OfType<LocalSmuNode>());
            Assert.Single(fixture.Editor.GetNodeGroups());
            Assert.Equal("restored-repeat", loaded.RepeatRegionId);
            Assert.Equal(3, loaded.RepeatCount);
            Assert.Equal(new double[] { -1.2, -1.3, -1.4 }, loaded.RepeatParameterValues[nameof(LocalSmuNode.MeasureValue)]);
            Assert.Equal(0, fixture.ConfirmCalls);
            Assert.Equal(fixture.A.Value.LoadedContentHash, fixture.DocumentBaseline);
            Assert.False(fixture.Editor.IsModified);
            Assert.False(fixture.Editor.CanUndo);
        });
    }

    [Theory]
    [InlineData(0)] // Programmatic selection preserves edits without prompting.
    [InlineData(1)] // Interactive selection is canceled.
    [InlineData(2)] // Refreshing the same template is also canceled.
    public void RejectedReplacementPreservesCanvasHistoryAndLoadedIdentity(int operation)
    {
        WithFixture(fixture =>
        {
            fixture.MakeDirty();
            STNode[] nodes = fixture.Editor.Nodes.Cast<STNode>().ToArray();
            byte[] canvas = fixture.Editor.GetCanvasData();
            var undo = fixture.Editor.UndoHistory.ToArray();
            var redo = fixture.Editor.RedoHistory.ToArray();
            object? baseline = fixture.DocumentBaseline;
            fixture.Confirm = () => false;

            if (operation == 2)
                PumpUntil(fixture.Controller.RefreshAsync(FlowWorkspaceReplacementMode.Confirm));
            else
            {
                var selection = fixture.Controller.SelectFlowTemplateAsync(fixture.B, replacementMode:
                    operation == 0 ? FlowWorkspaceReplacementMode.PreserveChanges : FlowWorkspaceReplacementMode.Confirm);
                PumpUntil(selection);
                Assert.Null(selection.Result.Snapshot);
                Assert.False(string.IsNullOrWhiteSpace(selection.Result.FailureReason));
            }

            Assert.Equal(operation == 0 ? 0 : 1, fixture.ConfirmCalls);
            Assert.Equal(0, fixture.CloseCalls);
            Assert.Equal(nodes, fixture.Editor.Nodes.Cast<STNode>());
            Assert.Equal(canvas, fixture.Editor.GetCanvasData());
            Assert.Equal(undo, fixture.Editor.UndoHistory);
            Assert.Equal(redo, fixture.Editor.RedoHistory);
            Assert.True(fixture.Editor.IsModified);
            Assert.Equal(baseline, fixture.DocumentBaseline);
            Assert.Same(fixture.A.Value, fixture.Controller.ActiveFlowParam);
            Assert.Same(fixture.A.Value, fixture.Manager.SelectedFlowParam);
            Assert.Equal(fixture.A.Id, fixture.Controller.RequestedTemplateId);
            Assert.Equal(0, fixture.Manager.TemplateFlowParamsIndex);
            Assert.Equal(fixture.A.Id, FlowEngineConfig.Instance.LastSelectFlow);
            Assert.False(fixture.Controller.IsConfirmingDocumentReplacement);
            var retained = fixture.Controller.WaitForExecutionSnapshotAsync();
            PumpUntil(retained);
            Assert.Equal(fixture.A.Id, retained.Result.Snapshot!.TemplateId);
        });
    }

    [Fact]
    public void FirstSaveDuringConfirmationContinuesOriginalSelection()
    {
        WithFixture(fixture =>
        {
            fixture.MakeDirty();
            Assert.Null(fixture.Controller.ActiveFlowParam);
            var saved = new TemplateModel<FlowParam>("Saved draft", new FlowParam
            {
                Id = 404,
                Name = "Saved draft",
                DataBase64 = Convert.ToBase64String(fixture.Editor.GetCanvasData()),
            });
            bool wasConfirming = false;
            long beforeAdopt = -1;
            long afterAdopt = -2;
            int? requestedAfterAdopt = null;
            FlowParam? adopted = null;
            fixture.Confirm = () =>
            {
                wasConfirming = fixture.Controller.IsConfirmingDocumentReplacement;
                TemplateFlow.Params.Add(saved);
                beforeAdopt = fixture.RequestGeneration;
                fixture.Controller.AdoptSavedCanvas(saved);
                afterAdopt = fixture.RequestGeneration;
                requestedAfterAdopt = fixture.Controller.RequestedTemplateId;
                adopted = fixture.Controller.ActiveFlowParam;
                fixture.View.SetDocumentBaseline(saved.Value);
                fixture.Editor.MarkSaved();
                return true;
            };

            var selection = fixture.Controller.SelectFlowTemplateAsync(fixture.B, replacementMode: FlowWorkspaceReplacementMode.Confirm);
            PumpUntil(selection);

            Assert.True(wasConfirming);
            Assert.Equal(beforeAdopt, afterAdopt);
            Assert.Equal(fixture.B.Id, requestedAfterAdopt);
            Assert.Same(saved.Value, adopted);
            var snapshot = Assert.IsType<FlowTemplateExecutionSnapshot>(selection.Result.Snapshot);
            Assert.Equal(fixture.B.Id, snapshot.TemplateId);
            Assert.True(fixture.Controller.IsCurrentExecutionSnapshot(snapshot));
            Assert.Same(fixture.B.Value, fixture.Controller.ActiveFlowParam);
            Assert.Same(fixture.B.Value, fixture.Manager.SelectedFlowParam);
            Assert.Equal(fixture.B.Id, fixture.Controller.RequestedTemplateId);
            Assert.Equal(1, fixture.Manager.TemplateFlowParamsIndex);
            Assert.Equal(fixture.B.Id, FlowEngineConfig.Instance.LastSelectFlow);
            Assert.Equal("B node", Assert.Single(fixture.Editor.Nodes.Cast<STNode>()).Title);
            Assert.False(fixture.Editor.IsModified);
            Assert.Equal(1, fixture.ConfirmCalls);
            Assert.Equal(1, fixture.CloseCalls);
            Assert.False(fixture.Controller.IsConfirmingDocumentReplacement);
        }, associateInitialTemplate: false);
    }

    [Fact]
    public void FirstSaveDuringConfirmationKeepsNewerQueuedSelectionAndRejectsOldRequest()
    {
        WithFixture(fixture =>
        {
            fixture.MakeDirty();
            Assert.Null(fixture.Controller.ActiveFlowParam);
            var saved = new TemplateModel<FlowParam>("Saved draft", new FlowParam
            {
                Id = 404,
                Name = "Saved draft",
                DataBase64 = Convert.ToBase64String(fixture.Editor.GetCanvasData()),
            });
            bool wasConfirming = false;
            long beforeAdopt = -1;
            long afterAdopt = -2;
            CancellationTokenSource? pendingBefore = null;
            CancellationTokenSource? pendingAfter = null;
            int? requestedAfterAdopt = null;
            FlowParam? adopted = null;
            fixture.Confirm = () =>
            {
                wasConfirming = fixture.Controller.IsConfirmingDocumentReplacement;
                TemplateFlow.Params.Add(saved);
                // A modal save can process a newer selection before adopting the saved canvas.
                fixture.Controller.OnFlowSelectionChanged(fixture.C);
                beforeAdopt = fixture.RequestGeneration;
                pendingBefore = fixture.PendingRefresh;
                fixture.Controller.AdoptSavedCanvas(saved);
                afterAdopt = fixture.RequestGeneration;
                pendingAfter = fixture.PendingRefresh;
                requestedAfterAdopt = fixture.Controller.RequestedTemplateId;
                adopted = fixture.Controller.ActiveFlowParam;
                fixture.View.SetDocumentBaseline(saved.Value);
                fixture.Editor.MarkSaved();
                return false;
            };

            var oldSelection = fixture.Controller.SelectFlowTemplateAsync(fixture.B, replacementMode: FlowWorkspaceReplacementMode.Confirm);
            PumpUntil(oldSelection);

            Assert.True(wasConfirming);
            Assert.Equal(beforeAdopt, afterAdopt);
            Assert.NotNull(pendingBefore);
            Assert.Same(pendingBefore, pendingAfter);
            Assert.False(pendingBefore.IsCancellationRequested);
            Assert.Equal(fixture.C.Id, requestedAfterAdopt);
            Assert.Same(saved.Value, adopted);
            Assert.Null(oldSelection.Result.Snapshot);
            Assert.Equal(fixture.C.Id, fixture.Controller.RequestedTemplateId);

            var latest = fixture.Controller.WaitForExecutionSnapshotAsync();
            PumpUntil(latest);
            Assert.Equal(fixture.C.Id, latest.Result.Snapshot!.TemplateId);
            Assert.Same(fixture.C.Value, fixture.Controller.ActiveFlowParam);
            Assert.Same(fixture.C.Value, fixture.Manager.SelectedFlowParam);
            Assert.Equal(2, fixture.Manager.TemplateFlowParamsIndex);
            Assert.Equal(fixture.C.Id, FlowEngineConfig.Instance.LastSelectFlow);
            Assert.Equal("C node", Assert.Single(fixture.Editor.Nodes.Cast<STNode>()).Title);
            Assert.Equal(1, fixture.CloseCalls);
            Assert.Equal(1, fixture.ConfirmCalls);
            Assert.False(fixture.Controller.IsConfirmingDocumentReplacement);
        }, associateInitialTemplate: false);
    }

    [Fact]
    public void ConsecutiveSelectionsInTheSameDispatcherTurnOnlyLoadTheLatestTemplate()
    {
        WithFixture(fixture =>
        {
            fixture.Controller.OnFlowSelectionChanged(fixture.B);
            fixture.Controller.OnFlowSelectionChanged(fixture.C);
            Assert.Same(fixture.A.Value, fixture.Controller.ActiveFlowParam);

            var latest = fixture.Controller.WaitForExecutionSnapshotAsync();
            PumpUntil(latest);

            Assert.Equal(fixture.C.Id, latest.Result.Snapshot!.TemplateId);
            Assert.Same(fixture.C.Value, fixture.Controller.ActiveFlowParam);
            Assert.Equal("C node", Assert.Single(fixture.Editor.Nodes.Cast<STNode>()).Title);
            Assert.Equal(1, fixture.CloseCalls);
        });
    }

    [Fact]
    public void AcceptedReplacementReturnsItsOwnSnapshotWhichExpiresAfterAnotherSelection()
    {
        WithFixture(fixture =>
        {
            fixture.MakeDirty();
            fixture.Confirm = () => true;
            var selectionB = fixture.Controller.SelectFlowTemplateAsync(fixture.B, replacementMode: FlowWorkspaceReplacementMode.Confirm);
            PumpUntil(selectionB);
            var snapshotB = Assert.IsType<FlowTemplateExecutionSnapshot>(selectionB.Result.Snapshot);
            Assert.Equal(fixture.B.Id, snapshotB.TemplateId);
            Assert.Equal(fixture.B.Value.DataBase64, snapshotB.DataBase64);
            Assert.True(fixture.Controller.IsCurrentExecutionSnapshot(snapshotB));
            Assert.Equal("B node", Assert.Single(fixture.Editor.Nodes.Cast<STNode>()).Title);
            Assert.False(fixture.Editor.IsModified);
            Assert.Empty(fixture.Editor.UndoHistory);

            var selectionC = fixture.Controller.SelectFlowTemplateAsync(fixture.C);
            PumpUntil(selectionC);
            var snapshotC = Assert.IsType<FlowTemplateExecutionSnapshot>(selectionC.Result.Snapshot);
            Assert.Equal(fixture.C.Id, snapshotC.TemplateId);
            Assert.False(fixture.Controller.IsCurrentExecutionSnapshot(snapshotB));
            Assert.True(fixture.Controller.IsCurrentExecutionSnapshot(snapshotC));
            Assert.Equal("C node", Assert.Single(fixture.Editor.Nodes.Cast<STNode>()).Title);
            Assert.Equal(1, fixture.ConfirmCalls);
            Assert.Equal(2, fixture.CloseCalls);
        });
    }

    [Fact]
    public void ReconcileRenumberedTemplateKeepsItsCanvasAndUpdatesExecutionIdentity()
    {
        WithFixture(fixture =>
        {
            FlowParam active = fixture.A.Value;
            active.FlowKey = "flow:active";
            fixture.B.Value.FlowKey = "flow:other";
            int originalId = active.Id;
            int newId = fixture.B.Id;
            var previous = fixture.Controller.WaitForExecutionSnapshotAsync();
            PumpUntil(previous);
            FlowTemplateExecutionSnapshot oldSnapshot = Assert.IsType<FlowTemplateExecutionSnapshot>(previous.Result.Snapshot);
            fixture.MakeDirty();
            byte[] canvas = fixture.Editor.GetCanvasData();

            // The MySQL order swap publishes the exchanged IDs on the same models.
            active.Id = newId;
            fixture.B.Value.Id = originalId;
            TemplateFlow.Params.Move(1, 0);
            PumpUntil(fixture.Controller.ReconcileTemplatesAsync());

            Assert.Same(active, fixture.Controller.ActiveFlowParam);
            Assert.Equal(canvas, fixture.Editor.GetCanvasData());
            Assert.True(fixture.Editor.IsModified);
            Assert.Equal(0, fixture.ConfirmCalls);
            Assert.Equal(0, fixture.CloseCalls);
            Assert.False(fixture.Controller.IsCurrentExecutionSnapshot(oldSnapshot));
            var current = fixture.Controller.WaitForExecutionSnapshotAsync();
            PumpUntil(current);
            FlowTemplateExecutionSnapshot snapshot = Assert.IsType<FlowTemplateExecutionSnapshot>(current.Result.Snapshot);
            Assert.Equal(newId, snapshot.TemplateId);
            Assert.Equal("flow:active", snapshot.FlowKey);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReconcileReloadedOrderDoesNotRetargetAnIdenticalDirtyCanvas(bool savedCanvasChanged)
    {
        WithFixture(fixture =>
        {
            FlowParam active = fixture.A.Value;
            active.FlowKey = "flow:active";
            int originalId = active.Id;
            int newId = fixture.B.Id;
            fixture.MakeDirty();
            byte[] canvas = fixture.Editor.GetCanvasData();
            var latest = new FlowParam
            {
                Id = newId,
                Name = active.Name,
                FlowKey = active.FlowKey,
                DataBase64 = savedCanvasChanged ? fixture.C.Value.DataBase64 : active.DataBase64,
            };

            // A database reload can reuse the wrappers by row ID after the stored IDs swap.
            fixture.A.Value = new FlowParam { Id = originalId, Name = "Other flow", FlowKey = "flow:other", DataBase64 = active.DataBase64 };
            fixture.B.Value = latest;
            PumpUntil(fixture.Controller.ReconcileTemplatesAsync());

            Assert.Equal(canvas, fixture.Editor.GetCanvasData());
            Assert.True(fixture.Editor.IsModified);
            Assert.Equal(savedCanvasChanged ? 1 : 0, fixture.ConfirmCalls);
            Assert.Equal(0, fixture.CloseCalls);
            var current = fixture.Controller.WaitForExecutionSnapshotAsync();
            PumpUntil(current);
            if (savedCanvasChanged)
            {
                Assert.Null(fixture.Controller.ActiveFlowParam);
                Assert.Null(fixture.Manager.SelectedFlowParam);
                Assert.Null(fixture.DocumentBaseline);
                Assert.Null(current.Result.Snapshot);
                return;
            }

            Assert.Same(latest, fixture.Controller.ActiveFlowParam);
            Assert.Same(latest, fixture.Manager.SelectedFlowParam);
            FlowTemplateExecutionSnapshot snapshot = Assert.IsType<FlowTemplateExecutionSnapshot>(current.Result.Snapshot);
            Assert.Equal(newId, snapshot.TemplateId);
            Assert.Equal("flow:active", snapshot.FlowKey);
        });
    }

    [Fact]
    public void ReconcileSameCanvasUpdatesMetadataAndStableSelectionWithoutTouchingEditingState()
    {
        WithFixture(fixture =>
        {
            fixture.MakeDirty();
            fixture.View.ShowExecutionSummary("Previous execution completed");
            fixture.Editor.MoveCanvas(73, 49, false, CanvasMoveArgs.All);
            STNode[] nodes = fixture.Editor.Nodes.Cast<STNode>().ToArray();
            byte[] canvas = fixture.Editor.GetCanvasData();
            var undo = fixture.Editor.UndoHistory.ToArray();
            var redo = fixture.Editor.RedoHistory.ToArray();
            object? baseline = fixture.DocumentBaseline;
            long generation = fixture.RequestGeneration;
            var latest = new FlowParam
            {
                Id = fixture.A.Id,
                Name = "Renamed A",
                DataBase64 = fixture.A.Value.DataBase64,
                TemplateRevision = 8,
                ResourceId = 81,
                FlowKey = "current-flow-key",
            };
            fixture.A.Value = latest;
            TemplateFlow.Params.Remove(fixture.C);
            TemplateFlow.Params.Move(0, 1);

            PumpUntil(fixture.Controller.ReconcileTemplatesAsync());

            Assert.Equal(0, fixture.ConfirmCalls);
            Assert.Equal(0, fixture.CloseCalls);
            Assert.Same(latest, fixture.Controller.ActiveFlowParam);
            Assert.Same(latest, fixture.Controller.RequestedFlowParam);
            Assert.Same(latest, fixture.Manager.SelectedFlowParam);
            Assert.Equal(fixture.A.Id, fixture.Controller.RequestedTemplateId);
            Assert.Equal(1, fixture.Manager.TemplateFlowParamsIndex);
            Assert.Equal(fixture.A.Id, FlowEngineConfig.Instance.LastSelectFlow);
            Assert.Equal(generation, fixture.RequestGeneration);
            Assert.Equal(nodes, fixture.Editor.Nodes.Cast<STNode>());
            Assert.Equal(canvas, fixture.Editor.GetCanvasData());
            Assert.Equal(undo, fixture.Editor.UndoHistory);
            Assert.Equal(redo, fixture.Editor.RedoHistory);
            Assert.True(fixture.Editor.IsModified);
            Assert.Equal(baseline, fixture.DocumentBaseline);
            Assert.Equal("Previous execution completed", fixture.View.logTextBox.Text);
            Assert.Equal(73, fixture.Editor.CanvasOffsetX);
            Assert.Equal(49, fixture.Editor.CanvasOffsetY);
            var snapshot = fixture.Controller.WaitForExecutionSnapshotAsync();
            PumpUntil(snapshot);
            Assert.Equal("Renamed A", snapshot.Result.Snapshot!.FlowName);
            Assert.Equal(8, snapshot.Result.Snapshot.TemplateRevision);
            Assert.Equal(81, snapshot.Result.Snapshot.ResourceId);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReconcileChangedSavedCanvasUsesReplacementConfirmation(bool accept)
    {
        WithFixture(fixture =>
        {
            fixture.MakeDirty();
            FlowParam previous = fixture.A.Value;
            STNode[] nodes = fixture.Editor.Nodes.Cast<STNode>().ToArray();
            byte[] canvas = fixture.Editor.GetCanvasData();
            var undo = fixture.Editor.UndoHistory.ToArray();
            object? baseline = fixture.DocumentBaseline;
            fixture.A.Value = new FlowParam
            {
                Id = previous.Id,
                Name = previous.Name,
                DataBase64 = fixture.B.Value.DataBase64,
            };
            fixture.Confirm = () => accept;

            PumpUntil(fixture.Controller.ReconcileTemplatesAsync());

            Assert.Equal(1, fixture.ConfirmCalls);
            Assert.Equal(accept ? 1 : 0, fixture.CloseCalls);
            if (accept)
            {
                Assert.Same(fixture.A.Value, fixture.Controller.ActiveFlowParam);
                Assert.Equal("B node", Assert.Single(fixture.Editor.Nodes.Cast<STNode>()).Title);
                Assert.False(fixture.Editor.IsModified);
                Assert.Empty(fixture.Editor.UndoHistory);
                Assert.NotEqual(baseline, fixture.DocumentBaseline);
            }
            else
            {
                Assert.Same(previous, fixture.Controller.ActiveFlowParam);
                Assert.Equal(nodes, fixture.Editor.Nodes.Cast<STNode>());
                Assert.Equal(canvas, fixture.Editor.GetCanvasData());
                Assert.Equal(undo, fixture.Editor.UndoHistory);
                Assert.True(fixture.Editor.IsModified);
                Assert.Equal(baseline, fixture.DocumentBaseline);
            }
            var snapshot = fixture.Controller.WaitForExecutionSnapshotAsync();
            PumpUntil(snapshot);
            Assert.Equal(accept ? fixture.A.Value.DataBase64 : previous.DataBase64, snapshot.Result.Snapshot!.DataBase64);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReconcilePreservesPendingOrLoadingSelection(bool loading)
    {
        WithFixture(fixture =>
        {
            var releaseClose = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<FlowTemplateExecutionSnapshotResult>? selection = null;
            if (loading)
            {
                fixture.Close = () => releaseClose.Task;
                selection = fixture.Controller.SelectFlowTemplateAsync(fixture.B);
                Assert.Equal(1, fixture.CloseCalls);
                Assert.False(selection.IsCompleted);
            }
            else
            {
                fixture.Controller.OnFlowSelectionChanged(fixture.B);
            }
            CancellationTokenSource? pending = fixture.PendingRefresh;
            long generation = fixture.RequestGeneration;
            try
            {
                Task reconcile = fixture.Controller.ReconcileTemplatesAsync();
                Assert.True(reconcile.IsCompleted);
                reconcile.GetAwaiter().GetResult();
                Assert.Equal(generation, fixture.RequestGeneration);
                Assert.Equal(fixture.B.Id, fixture.Controller.RequestedTemplateId);
                Assert.Same(pending, fixture.PendingRefresh);
                if (pending != null) Assert.False(pending.IsCancellationRequested);
                Assert.Equal(0, fixture.ConfirmCalls);
                Assert.Equal(loading ? 1 : 0, fixture.CloseCalls);
            }
            finally
            {
                releaseClose.TrySetResult();
                if (selection != null) PumpUntil(selection);
            }
            var snapshot = fixture.Controller.WaitForExecutionSnapshotAsync();
            PumpUntil(snapshot);
            Assert.Equal(fixture.B.Id, snapshot.Result.Snapshot!.TemplateId);
            Assert.Same(fixture.B.Value, fixture.Controller.ActiveFlowParam);
            Assert.Equal("B node", Assert.Single(fixture.Editor.Nodes.Cast<STNode>()).Title);
            Assert.Equal(1, fixture.CloseCalls);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PendingSelectionCannotSwitchToAnotherFlowReusingItsId(bool targetRemainsAvailable)
    {
        WithFixture(fixture =>
        {
            fixture.B.Value.FlowKey = "flow:requested";
            fixture.C.Value.FlowKey = "flow:other";
            FlowParam requested = fixture.B.Value;
            FlowParam previous = fixture.A.Value;
            var releaseClose = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Close = () => releaseClose.Task;
            var selection = fixture.Controller.SelectFlowTemplateAsync(fixture.B);
            Assert.False(selection.IsCompleted);

            // TemplateFlow.ApplyValues reuses the wrapper at a numeric ID.
            int oldId = requested.Id;
            int newId = fixture.C.Id;
            fixture.B.Value = new FlowParam
            {
                Id = oldId, Name = fixture.C.Key, FlowKey = fixture.C.Value.FlowKey,
                DataBase64 = fixture.C.Value.DataBase64
            };
            TemplateFlow.Params.Remove(fixture.C);
            if (targetRemainsAvailable)
            {
                TemplateFlow.Params.Add(new TemplateModel<FlowParam>(requested.Name, new FlowParam
                {
                    Id = newId, Name = requested.Name, FlowKey = requested.FlowKey,
                    DataBase64 = requested.DataBase64
                }));
            }

            releaseClose.SetResult();
            PumpUntil(selection);
            var current = fixture.Controller.WaitForExecutionSnapshotAsync();
            PumpUntil(current);
            if (targetRemainsAvailable)
            {
                Assert.Equal(requested.FlowKey, selection.Result.Snapshot!.FlowKey);
                Assert.Equal(newId, current.Result.Snapshot!.TemplateId);
                Assert.Equal(newId, fixture.Controller.RequestedTemplateId);
                Assert.Equal(newId, FlowEngineConfig.Instance.LastSelectFlow);
                Assert.Equal("B node", Assert.Single(fixture.Editor.Nodes.Cast<STNode>()).Title);
            }
            else
            {
                Assert.Null(selection.Result.Snapshot);
                Assert.Null(current.Result.Snapshot);
                Assert.Same(previous, fixture.Controller.ActiveFlowParam);
                Assert.Equal(previous.Id, fixture.Controller.RequestedTemplateId);
                Assert.Equal(previous.Id, FlowEngineConfig.Instance.LastSelectFlow);
                Assert.Equal("A node", Assert.Single(fixture.Editor.Nodes.Cast<STNode>()).Title);
            }
        });
    }

    [Fact]
    public void ReconcileDeletedLastTemplateKeepsCanceledDirtyDraftWithoutDeletedSaveIdentity()
    {
        WithFixture(fixture =>
        {
            fixture.MakeDirty();
            STNode[] nodes = fixture.Editor.Nodes.Cast<STNode>().ToArray();
            byte[] canvas = fixture.Editor.GetCanvasData();
            var undo = fixture.Editor.UndoHistory.ToArray();
            var redo = fixture.Editor.RedoHistory.ToArray();
            TemplateFlow.Params.Clear();
            fixture.Confirm = () =>
            {
                Assert.True(fixture.Controller.IsConfirmingDocumentReplacement);
                Assert.Null(fixture.Controller.ActiveFlowParam);
                Assert.Null(fixture.Manager.SelectedFlowParam);
                Assert.Null(fixture.DocumentBaseline);
                return false;
            };

            PumpUntil(fixture.Controller.ReconcileTemplatesAsync());

            Assert.Equal(1, fixture.ConfirmCalls);
            Assert.Equal(0, fixture.CloseCalls);
            Assert.Null(fixture.Controller.ActiveFlowParam);
            Assert.Null(fixture.Controller.RequestedFlowParam);
            Assert.Null(fixture.Controller.RequestedTemplateId);
            Assert.Null(fixture.Manager.SelectedFlowParam);
            Assert.Null(fixture.DocumentBaseline);
            Assert.Equal(-1, fixture.Manager.TemplateFlowParamsIndex);
            Assert.Equal(nodes, fixture.Editor.Nodes.Cast<STNode>());
            Assert.Equal(canvas, fixture.Editor.GetCanvasData());
            Assert.Equal(undo, fixture.Editor.UndoHistory);
            Assert.Equal(redo, fixture.Editor.RedoHistory);
            Assert.True(fixture.Editor.IsModified);
            var snapshot = fixture.Controller.WaitForExecutionSnapshotAsync();
            PumpUntil(snapshot);
            Assert.Null(snapshot.Result.Snapshot);
            Assert.False(string.IsNullOrWhiteSpace(snapshot.Result.FailureReason));
        });
    }

    [Fact]
    public void ReconcileDoesNotTreatUnlistedProgrammaticTemplateAsDeleted()
    {
        WithFixture(fixture =>
        {
            var unlisted = new TemplateModel<FlowParam>("Programmatic flow", new FlowParam
            {
                Id = 505,
                Name = "Programmatic flow",
                DataBase64 = fixture.A.Value.DataBase64,
            });
            fixture.Controller.AdoptSavedCanvas(unlisted);
            long generation = fixture.RequestGeneration;
            STNode[] nodes = fixture.Editor.Nodes.Cast<STNode>().ToArray();

            PumpUntil(fixture.Controller.ReconcileTemplatesAsync());

            Assert.Equal(0, fixture.ConfirmCalls);
            Assert.Equal(0, fixture.CloseCalls);
            Assert.Equal(generation, fixture.RequestGeneration);
            Assert.Equal(nodes, fixture.Editor.Nodes.Cast<STNode>());
            Assert.Same(unlisted.Value, fixture.Controller.ActiveFlowParam);
            Assert.Same(unlisted.Value, fixture.Manager.SelectedFlowParam);
            var snapshot = fixture.Controller.WaitForExecutionSnapshotAsync();
            PumpUntil(snapshot);
            Assert.Equal(unlisted.Id, snapshot.Result.Snapshot!.TemplateId);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnavailableLocalSelectionDoesNotImplicitlySwitchToAnotherFlow(bool alreadyLoaded)
    {
        string path = Path.Combine(Path.GetTempPath(), "ColorVision-workspace-unavailable-tests", Guid.NewGuid().ToString("N"), "local.db");
        var documents = new LocalTemplateStore(path);
        var storage = new LocalFlowTemplateStorage(documents);
        WithFixture(fixture =>
        {
            var damaged = new FlowParam { Name = "Damaged flow", DataBase64 = fixture.A.Value.DataBase64 };
            var healthy = new FlowParam { Name = "Healthy flow", DataBase64 = fixture.B.Value.DataBase64 };
            storage.Save(damaged);
            storage.Save(healthy);
            TemplateFlow templates = fixture.Manager.CreateFlowTemplate();
            templates.Load();

            FlowTemplateExecutionSnapshot? previous = null;
            if (alreadyLoaded)
            {
                var load = fixture.Controller.SelectFlowTemplateAsync(
                    TemplateFlow.Params[0], allowEmptyFlow: true, replacementMode: FlowWorkspaceReplacementMode.AlreadyConfirmed);
                PumpUntil(load);
                previous = Assert.IsType<FlowTemplateExecutionSnapshot>(load.Result.Snapshot);
            }

            FlowEngineConfig.Instance.LastSelectFlow = damaged.Id;
            documents.Save("flow", -damaged.Id - 1, damaged.Name, 99, "{}");
            templates.Load();
            var available = Assert.Single(TemplateFlow.Params);
            Assert.Equal(healthy.Id, available.Id);

            if (alreadyLoaded)
            {
                Assert.False(fixture.Controller.IsCurrentExecutionSnapshot(previous!));
                PumpUntil(fixture.Controller.ReconcileTemplatesAsync());
            }
            else
            {
                fixture.Controller.InitializeSelection();
            }

            Assert.Null(fixture.Manager.SelectedFlowParam);
            Assert.Equal(-1, fixture.Manager.TemplateFlowParamsIndex);
            Assert.Equal(damaged.Id, FlowEngineConfig.Instance.LastSelectFlow);
            var failed = fixture.Controller.WaitForExecutionSnapshotAsync();
            PumpUntil(failed);
            Assert.Null(failed.Result.Snapshot);
            Assert.False(string.IsNullOrWhiteSpace(failed.Result.FailureReason));

            var explicitSelection = fixture.Controller.SelectFlowTemplateAsync(
                available, allowEmptyFlow: true, replacementMode: FlowWorkspaceReplacementMode.AlreadyConfirmed);
            PumpUntil(explicitSelection);
            Assert.Equal(healthy.Id, explicitSelection.Result.Snapshot!.TemplateId);
        }, associateInitialTemplate: false, createFlowTemplate: () => new TemplateFlow(storage, () => false,
            () => throw new InvalidOperationException("This workflow test must not open MySQL.")));
    }

    [Fact]
    public void PersistedComboIndexDoesNotSelectAnotherFlowWhenRememberedLocalTemplateIsUnavailable()
    {
        string path = Path.Combine(Path.GetTempPath(), "ColorVision-workspace-unavailable-tests", Guid.NewGuid().ToString("N"), "local.db");
        var documents = new LocalTemplateStore(path);
        var storage = new LocalFlowTemplateStorage(documents);
        var damaged = new FlowParam { Name = "Damaged flow" };
        var healthy = new FlowParam { Name = "Healthy flow" };
        storage.Save(damaged);
        storage.Save(healthy);
        documents.Save("flow", -damaged.Id - 1, damaged.Name, 99, "{}");
        TemplateFlow CreateTemplate() => new(storage, () => false,
            () => throw new InvalidOperationException("This workflow test must not open MySQL."));
        WithFixture(fixture =>
        {
            Assert.Equal(healthy.Id, Assert.Single(TemplateFlow.Params).Id);
            var picker = Assert.IsAssignableFrom<System.Windows.Controls.ComboBox>(
                fixture.Manager.DisplayFlow.FindName("ComboBoxFlow"));
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
            Assert.Null(picker.SelectedItem);
            Assert.Equal(damaged.Id, FlowEngineConfig.Instance.LastSelectFlow);
            Assert.Equal(-1, fixture.Manager.TemplateFlowParamsIndex);
            Assert.Null(fixture.Manager.SelectedFlowParam);
        }, associateInitialTemplate: false, createFlowTemplate: CreateTemplate, prepareBeforeManager: () =>
        {
            CreateTemplate().Load();
            FlowEngineConfig.Instance.LastSelectFlow = damaged.Id;
            FlowEngineConfig.Instance.TemplateFlowParamsIndex = 0;
        });
    }

    private static void WithFixture(Action<WorkspaceFixture> test, bool associateInitialTemplate = true,
        Func<TemplateFlow>? createFlowTemplate = null, Action? prepareBeforeManager = null)
    {
        WpfTestHost.Invoke(() =>
        {
            var previousConfig = ConfigService.Instance;
            var previousTemplates = TemplateFlow.Params;
            TestEnvironment.SetConfig(new ConfigHandler { IsAutoSave = false });
            TemplateFlow.Params = [];
            FlowEngineConfig.Instance.TemplateFlowParamsIndex = -1;
            var theme = new ResourceDictionary { Source = new Uri("/ColorVision.Themes;component/Themes/Theme.xaml", UriKind.Relative) };
            Application.Current.Resources.MergedDictionaries.Add(theme);
            try
            {
                Assert.False(MySqlSetting.IsConnect);
                Assert.Null(ReadField(MqttRCService.GetInstance(), "Token"));
                using var fixture = new WorkspaceFixture(associateInitialTemplate, createFlowTemplate, prepareBeforeManager);
                test(fixture);
            }
            finally
            {
                Application.Current.Resources.MergedDictionaries.Remove(theme);
                TemplateFlow.Params = previousTemplates;
                TestEnvironment.SetConfig(previousConfig);
            }
        });
    }

    private static object? ReadField(object owner, string name)
        => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner);

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

    private sealed class WorkspaceFixture : IDisposable
    {
        public FlowEngineManager Manager { get; }
        public ViewFlow View => Manager.View;
        public STNodeEditor Editor => View.STNodeEditorMain;
        public FlowTemplateWorkspaceController Controller { get; }
        public TemplateModel<FlowParam> A { get; } = CreateTemplate(101, "A");
        public TemplateModel<FlowParam> B { get; } = CreateTemplate(202, "B");
        public TemplateModel<FlowParam> C { get; } = CreateTemplate(303, "C");
        public Func<bool> Confirm { get; set; } = () => false;
        public Func<Task> Close { get; set; } = () => Task.CompletedTask;
        public int ConfirmCalls { get; private set; }
        public int CloseCalls { get; private set; }
        public object? DocumentBaseline => ReadField(View, "_documentLoadedContentHash");
        public long RequestGeneration => (long)ReadField(ReadField(Controller, "_refreshGate")!, "_generation")!;
        public CancellationTokenSource? PendingRefresh => (CancellationTokenSource?)ReadField(Controller, "_refreshCts");

        public WorkspaceFixture(bool associateInitialTemplate, Func<TemplateFlow>? createFlowTemplate, Action? prepareBeforeManager)
        {
            foreach (var template in new[] { A, B, C }) TemplateFlow.Params.Add(template);
            prepareBeforeManager?.Invoke();
            Manager = new FlowEngineManager(createFlowTemplate);
            var session = (FlowExecutionSession)ReadField(View, "_executionSession")!;
            ((FlowTemplateWorkspaceController)ReadField(session, "_templateWorkspace")!).Dispose();
            Controller = new FlowTemplateWorkspaceController(Manager, View,
                () => { CloseCalls++; return Close(); },
                () => { }, () => { }, _ => { }, _ => { },
                () => { ConfirmCalls++; return Confirm(); });
            typeof(FlowExecutionSession).GetField("_templateWorkspace", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, Controller);
            View.FlowEngineControl.LoadFromBase64(A.Value.DataBase64);
            if (associateInitialTemplate)
            {
                Controller.AdoptSavedCanvas(A);
                View.SetDocumentBaseline(A.Value);
            }
        }

        public void MakeDirty()
        {
            Editor.ExecuteEditTransaction("Move draft node", () => Editor.Nodes[0].Left += 80);
            var extra = new STNodeHub();
            extra.Create();
            Editor.Nodes.Add(extra);
            Editor.Undo();
            Assert.True(Editor.IsModified);
            Assert.True(Editor.CanUndo);
            Assert.True(Editor.CanRedo);
        }

        public void Dispose()
        {
            View.Dispose();
            Manager.DisplayFlow.Dispose();
            Manager.FlowEngineControl.Dispose();
            DockViewManager.GetInstance().RemoveView(View);
            (ReadField(Manager.Copilot, "_copilotAgentExtensionRegistration") as IDisposable)?.Dispose();
        }

        private static TemplateModel<FlowParam> CreateTemplate(int id, string name)
        {
            using var editor = new STNodeEditor();
            var node = new STNodeHub { Title = name + " node" };
            node.Create();
            editor.Nodes.Add(node);
            return new TemplateModel<FlowParam>(name, new FlowParam { Id = id, Name = name, DataBase64 = Convert.ToBase64String(editor.GetCanvasData()) });
        }
    }
}
