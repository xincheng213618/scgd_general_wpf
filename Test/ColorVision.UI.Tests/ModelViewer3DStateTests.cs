using ColorVision.ImageEditor.EditorTools.ThreeD;

namespace ColorVision.UI.Tests;

public class ModelViewer3DStateTests
{
    [Fact]
    public void VisibilityState_ExitIsolationRestoresPriorHiddenState()
    {
        Guid root = Guid.NewGuid();
        Guid selected = Guid.NewGuid();
        Guid hidden = Guid.NewGuid();

        SceneVisibilityState state = new();
        state.SetHidden(new[] { hidden }, true);
        state.EnterIsolation(new[] { root, selected });

        Assert.True(state.IsVisible(root));
        Assert.True(state.IsVisible(selected));
        Assert.False(state.IsVisible(hidden));
        Assert.True(state.IsIsolated);

        state.ExitIsolation();

        Assert.True(state.IsVisible(root));
        Assert.True(state.IsVisible(selected));
        Assert.False(state.IsVisible(hidden));
        Assert.False(state.IsIsolated);
    }

    [Fact]
    public void VisibilityState_ShowAllClearsHiddenAndIsolationFilters()
    {
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        SceneVisibilityState state = new();
        state.SetHidden(new[] { first }, true);
        state.EnterIsolation(new[] { second });

        state.ShowAll();

        Assert.True(state.IsVisible(first));
        Assert.True(state.IsVisible(second));
        Assert.False(state.IsIsolated);
    }

    [Fact]
    public void VisibilityState_CopyRestoresHiddenAndIsolationFilters()
    {
        Guid root = Guid.NewGuid();
        Guid selected = Guid.NewGuid();
        Guid hidden = Guid.NewGuid();
        SceneVisibilityState original = new();
        original.SetHidden(new[] { hidden }, true);
        original.EnterIsolation(new[] { root, selected });

        SceneVisibilityState snapshot = original.Clone();
        original.ShowAll();
        original.CopyFrom(snapshot);

        Assert.True(original.IsVisible(root));
        Assert.True(original.IsVisible(selected));
        Assert.False(original.IsVisible(hidden));
        Assert.True(original.IsIsolated);
    }

    [Fact]
    public void VisibilityState_IsolationTemporarilyOverridesBaselineHiddenState()
    {
        Guid selected = Guid.NewGuid();
        SceneVisibilityState state = new();
        state.SetHidden(new[] { selected }, true);

        state.EnterIsolation(new[] { selected });
        Assert.True(state.IsVisible(selected));

        state.ExitIsolation();
        Assert.False(state.IsVisible(selected));
    }

    [Fact]
    public void Sessions_CopyDefaultsWithoutSharingDocumentState()
    {
        ModelViewerDefaults defaults = new(ModelViewerRenderMode.Solid, ModelViewerProjection.Orthographic);
        ModelViewer3DSession first = new(defaults);
        ModelViewer3DSession second = new(defaults);

        first.BeginLoad("first.obj");
        first.CompleteLoad("first.obj");
        first.RenderMode = ModelViewerRenderMode.Wireframe;
        first.Projection = ModelViewerProjection.Perspective;
        first.SelectedNodeId = Guid.NewGuid();

        Assert.Equal(ModelViewerRenderMode.Solid, second.RenderMode);
        Assert.Equal(ModelViewerProjection.Orthographic, second.Projection);
        Assert.Null(second.CurrentPath);
        Assert.Null(second.SelectedNodeId);
        Assert.Equal(ModelViewerLoadState.Empty, second.LoadState);
    }

    [Fact]
    public void Session_FailedOrCanceledReplacementKeepsPreviousReadyDocument()
    {
        ModelViewer3DSession session = new(new ModelViewerDefaults(ModelViewerRenderMode.Textured, ModelViewerProjection.Perspective));
        session.BeginLoad("first.obj");
        session.CompleteLoad("first.obj");

        session.BeginLoad("broken.obj");
        session.FailLoad("parse failed");
        Assert.Equal("first.obj", session.CurrentPath);
        Assert.Equal(ModelViewerLoadState.Ready, session.LoadState);
        Assert.Equal("parse failed", session.ErrorMessage);

        session.BeginLoad("second.obj");
        session.CancelLoad();
        Assert.Equal("first.obj", session.CurrentPath);
        Assert.Equal(ModelViewerLoadState.Ready, session.LoadState);
        Assert.Null(session.PendingPath);
    }

    [Fact]
    public void OrientationHeuristic_FlagsTheRealChairSampleButRejectsWeakEvidence()
    {
        Assert.True(ModelOrientationHeuristics.ShouldFlipVertical(573.26f, 546.16f, 830.26f, 19_471, 11_199));
        Assert.False(ModelOrientationHeuristics.ShouldFlipVertical(573.26f, 546.16f, 600f, 19_471, 11_199));
        Assert.False(ModelOrientationHeuristics.ShouldFlipVertical(573.26f, 546.16f, 830.26f, 80, 40));
        Assert.False(ModelOrientationHeuristics.ShouldFlipVertical(573.26f, 546.16f, 830.26f, 19_471, 19_000));
    }

    [Fact]
    public async Task LoadCoordinator_SecondRequestWinsWhenFirstCompletesLast()
    {
        using LatestModelLoadCoordinator<FakeScene> coordinator = new();
        TaskCompletionSource firstStarted = NewSignal();
        TaskCompletionSource<FakeScene> firstCompletion = NewSceneSignal();
        TaskCompletionSource<FakeScene> secondCompletion = NewSceneSignal();
        CancellationToken firstToken = default;

        Task<ModelLoadOperationResult<FakeScene>> firstTask = coordinator.RunAsync(async token =>
        {
            firstToken = token;
            firstStarted.SetResult();
            return await firstCompletion.Task;
        });
        await firstStarted.Task;

        Task<ModelLoadOperationResult<FakeScene>> secondTask = coordinator.RunAsync(_ => secondCompletion.Task);
        Assert.True(firstToken.IsCancellationRequested);

        FakeScene secondScene = new();
        secondCompletion.SetResult(secondScene);
        ModelLoadOperationResult<FakeScene> secondResult = await secondTask;

        FakeScene firstScene = new();
        firstCompletion.SetResult(firstScene);
        ModelLoadOperationResult<FakeScene> firstResult = await firstTask;

        Assert.Equal(ModelLoadOperationStatus.Succeeded, secondResult.Status);
        Assert.Same(secondScene, secondResult.Value);
        Assert.Equal(ModelLoadOperationStatus.Superseded, firstResult.Status);
        Assert.Equal(1, firstScene.DisposeCount);
        Assert.Equal(0, secondScene.DisposeCount);

        secondResult.Value!.Dispose();
    }

    [Fact]
    public async Task LoadCoordinator_DisposeRejectsLatePublicationAndDisposesItsScene()
    {
        LatestModelLoadCoordinator<FakeScene> coordinator = new();
        TaskCompletionSource<FakeScene> completion = NewSceneSignal();
        Task<ModelLoadOperationResult<FakeScene>> operation = coordinator.RunAsync(_ => completion.Task);
        coordinator.Dispose();

        FakeScene lateScene = new();
        completion.SetResult(lateScene);
        ModelLoadOperationResult<FakeScene> result = await operation;

        Assert.Equal(ModelLoadOperationStatus.Superseded, result.Status);
        Assert.Equal(1, lateScene.DisposeCount);
    }

    [Fact]
    public async Task LoadCoordinator_CancelActiveReturnsCanceledForCurrentRequest()
    {
        using LatestModelLoadCoordinator<FakeScene> coordinator = new();
        TaskCompletionSource started = NewSignal();

        Task<ModelLoadOperationResult<FakeScene>> operation = coordinator.RunAsync(async token =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new FakeScene();
        });
        await started.Task;
        coordinator.CancelActive();

        ModelLoadOperationResult<FakeScene> result = await operation;

        Assert.Equal(ModelLoadOperationStatus.Canceled, result.Status);
        Assert.Null(result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task LoadCoordinator_CancelDisposesAResultFromLoaderThatIgnoredItsToken()
    {
        using LatestModelLoadCoordinator<FakeScene> coordinator = new();
        TaskCompletionSource<FakeScene> completion = NewSceneSignal();
        Task<ModelLoadOperationResult<FakeScene>> operation = coordinator.RunAsync(_ => completion.Task);

        coordinator.CancelActive();
        FakeScene ignoredCancellationScene = new();
        completion.SetResult(ignoredCancellationScene);

        ModelLoadOperationResult<FakeScene> result = await operation;

        Assert.Equal(ModelLoadOperationStatus.Canceled, result.Status);
        Assert.Equal(1, ignoredCancellationScene.DisposeCount);
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static TaskCompletionSource<FakeScene> NewSceneSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class FakeScene : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
        }
    }
}
