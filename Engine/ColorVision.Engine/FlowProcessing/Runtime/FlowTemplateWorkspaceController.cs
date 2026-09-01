using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using FlowEngineLib.Base;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.FlowProcessing;

internal sealed class FlowTemplateWorkspaceController : IDisposable
{
    private static readonly ILog log =
        LogManager.GetLogger(typeof(FlowTemplateWorkspaceController));

    private readonly FlowEngineManager _flowEngineManager;
    private readonly ViewFlow _view;
    private readonly Func<Task> _closeRunningFlowBeforeRefreshAsync;
    private readonly Action _invalidateExecutionPresentation;
    private readonly Action _resetNodeTitleProgress;
    private readonly Action<CVCommonNode> _unsubscribeNodeEvents;
    private readonly Action<CVCommonNode> _subscribeNodeEvents;
    private readonly FlowTemplateRefreshGate _refreshGate = new();
    private readonly FlowTemplateWorkspaceState _workspaceState = new();
    private readonly object _selectionSync = new();
    private readonly List<CVBaseServerNode> _workspaceServerNodes = [];
    private CancellationTokenSource? _refreshCts;
    private bool _suppressSelectionRefresh;
    private TemplateModel<FlowParam>? _requestedTemplate;
    private FlowParam? _loadedFlowParam;
    private string? _startNodeName;
    private bool _disposed;

    public FlowTemplateWorkspaceController(
        FlowEngineManager flowEngineManager,
        ViewFlow view,
        Func<Task> closeRunningFlowBeforeRefreshAsync,
        Action invalidateExecutionPresentation,
        Action resetNodeTitleProgress,
        Action<CVCommonNode> unsubscribeNodeEvents,
        Action<CVCommonNode> subscribeNodeEvents)
    {
        _flowEngineManager = flowEngineManager;
        _view = view;
        _closeRunningFlowBeforeRefreshAsync =
            closeRunningFlowBeforeRefreshAsync;
        _invalidateExecutionPresentation =
            invalidateExecutionPresentation;
        _resetNodeTitleProgress = resetNodeTitleProgress;
        _unsubscribeNodeEvents = unsubscribeNodeEvents;
        _subscribeNodeEvents = subscribeNodeEvents;
    }

    public void InitializeSelection()
    {
        TemplateModel<FlowParam>? selectedTemplate =
            TemplateFlow.Params.FirstOrDefault(
                item =>
                    item.Id
                    == FlowEngineConfig.Instance.LastSelectFlow)
            ?? TemplateFlow.Params.FirstOrDefault();
        if (selectedTemplate == null)
            return;

        long generation;
        _suppressSelectionRefresh = true;
        try
        {
            generation = SetSelectedFlowTemplate(selectedTemplate);
        }
        finally
        {
            _suppressSelectionRefresh = false;
        }
        _ = RefreshAsync(generation, allowEmptyFlow: false);
    }

    public void OnFlowSelectionChanged(
        TemplateModel<FlowParam>? flowTemplate)
    {
        if (flowTemplate == null)
            return;

        long generation = SetSelectedFlowTemplate(flowTemplate);
        if (!_suppressSelectionRefresh)
            _ = DebouncedRefreshAsync(generation);
    }

    public async Task SelectFlowTemplateAsync(
        TemplateModel<FlowParam> flowTemplate,
        bool allowEmptyFlow = false)
    {
        CancelPendingRefresh();

        long generation;
        _suppressSelectionRefresh = true;
        try
        {
            generation = SetSelectedFlowTemplate(flowTemplate);
        }
        finally
        {
            _suppressSelectionRefresh = false;
        }

        await RefreshAsync(generation, allowEmptyFlow);
    }

    public Task RefreshAsync()
    {
        CancelPendingRefresh();
        long generation = _refreshGate.Advance();
        lock (_selectionSync)
        {
            _workspaceState.BeginRequest(
                generation,
                _requestedTemplate?.Id);
        }
        return RefreshAsync(generation, allowEmptyFlow: false);
    }

    private Task RefreshAsync(
        long generation,
        bool allowEmptyFlow)
    {
        return _refreshGate.ExecuteLatestAsync(
            generation,
            isCurrent => RefreshCoreAsync(
                generation,
                allowEmptyFlow,
                isCurrent));
    }

    private async Task RefreshCoreAsync(
        long generation,
        bool allowEmptyFlow,
        Func<bool> isCurrent)
    {
        byte[]? previousCanvasData = null;
        bool attemptedCanvasReplacement = false;
        try
        {
            if (!_workspaceState.TryMarkLoading(generation))
                return;

            await _closeRunningFlowBeforeRefreshAsync();
            if (!isCurrent() || _disposed)
                return;

            MqttRCService.GetInstance().QueryServices();
            if (!isCurrent() || _disposed)
                return;

            TemplateModel<FlowParam>? selectedTemplate =
                GetRequestedFlowTemplate();
            if (selectedTemplate == null)
            {
                if (isCurrent())
                {
                    ClearDisplayedFlow(null);
                    _workspaceState.TryCompleteEmpty(generation);
                }
                return;
            }

            FlowParam flowParam = selectedTemplate.Value;
            if (string.IsNullOrEmpty(flowParam.DataBase64))
            {
                if (!allowEmptyFlow)
                {
                    MessageBox.Show(
                        ColorVision.Engine.Properties.Resources
                            .Flow_CreateTemplateBeforeSelection);
                }
                if (isCurrent())
                {
                    ClearDisplayedFlow(flowParam);
                    _workspaceState.TryCompleteLoaded(
                        generation,
                        flowParam.Id);
                }
                return;
            }

            previousCanvasData =
                _view.STNodeEditorMain.GetCanvasData();
            CVCommonNode[] previousNodes =
                _view.STNodeEditorMain.Nodes
                    .OfType<CVCommonNode>()
                    .ToArray();
            attemptedCanvasReplacement = true;
            _view.FlowEngineControl.LoadFromBase64(
                flowParam.DataBase64,
                MqttRCService.GetInstance().ServiceTokens);
            foreach (CVCommonNode node in previousNodes)
                _unsubscribeNodeEvents(node);
            if (!isCurrent() || _disposed)
                return;

            _invalidateExecutionPresentation();
            _view.ShowExecutionSummary(string.Empty);
            IList<CVBaseServerNode> serverNodes =
                _view.IsStandalone
                    ? _workspaceServerNodes
                    : _flowEngineManager.CVBaseServerNodes;
            serverNodes.Clear();
            _resetNodeTitleProgress();
            _view.SetDocumentBaseline(flowParam);
            _view.FitLoadedFlowToViewport();
            RefreshStartNodeSelection();

            lock (_selectionSync)
                _loadedFlowParam = flowParam;
            if (!_view.IsStandalone)
                _flowEngineManager.SelectedFlowParam = flowParam;

            foreach (CVCommonNode node in
                _view.STNodeEditorMain.Nodes
                    .OfType<CVCommonNode>())
            {
                if (node is CVBaseServerNode serverNode)
                    serverNodes.Insert(0, serverNode);
                _subscribeNodeEvents(node);
            }
            _view.STNodeEditorMain.Invalidate();
            _flowEngineManager.Copilot.PublishContext();
            _workspaceState.TryCompleteLoaded(
                generation,
                flowParam.Id);
        }
        catch (Exception ex)
        {
            log.Error(ex);
            if (isCurrent() && !_disposed)
            {
                if (attemptedCanvasReplacement
                    && previousCanvasData != null)
                {
                    RestorePreviousCanvas(previousCanvasData);
                }
                RestoreSelectionAfterFailedRefresh(generation);
                Application.Current.Dispatcher.Invoke(
                    () =>
                    {
                        MessageBox.Show(
                            Application.Current.GetActiveWindow(),
                            ex.Message);
                    });
            }
        }
    }

    private TemplateModel<FlowParam>? GetRequestedFlowTemplate()
    {
        TemplateModel<FlowParam>? requestedTemplate;
        lock (_selectionSync)
            requestedTemplate = _requestedTemplate;
        if (requestedTemplate != null)
        {
            return TemplateFlow.Params.FirstOrDefault(
                    item => item.Id == requestedTemplate.Id)
                ?? requestedTemplate;
        }

        return null;
    }

    public async Task<FlowTemplateExecutionSnapshotResult>
        WaitForExecutionSnapshotAsync()
    {
        if (_view.IsStandalone)
        {
            TemplateModel<FlowParam>? standaloneTemplate =
                _view.GetStandaloneExecutionTemplate();
            return standaloneTemplate == null
                ? FlowTemplateExecutionSnapshotResult.Failed(
                    ColorVision.Engine.Properties.Resources
                        .Flow_NoValidFlowTemplateSelected)
                : FlowTemplateExecutionSnapshotResult.Loaded(
                    FlowTemplateExecutionSnapshot.Create(
                        generation: 0,
                        standaloneTemplate.Value));
        }

        while (true)
        {
            FlowTemplateWorkspaceSettlement settlement =
                await _workspaceState.WaitForCurrentSettlementAsync();
            await _refreshGate.WaitUntilIdleAsync();
            if (settlement.Status
                != FlowTemplateWorkspaceStatus.Loaded)
            {
                return FlowTemplateExecutionSnapshotResult.Failed(
                    settlement.FailureReason
                    ?? ColorVision.Engine.Properties.Resources
                        .Flow_NoValidFlowTemplateSelected);
            }

            lock (_selectionSync)
            {
                if (!_workspaceState.IsCurrentLoaded(
                        settlement.Generation,
                        settlement.TemplateId))
                {
                    continue;
                }
                if (_loadedFlowParam == null
                    || _loadedFlowParam.Id != settlement.TemplateId)
                {
                    return FlowTemplateExecutionSnapshotResult.Failed(
                        "流程工作区状态不一致，请刷新后重试。");
                }

                return FlowTemplateExecutionSnapshotResult.Loaded(
                    FlowTemplateExecutionSnapshot.Create(
                        settlement.Generation,
                        _loadedFlowParam));
            }
        }
    }

    public bool IsCurrentExecutionSnapshot(
        FlowTemplateExecutionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return _view.IsStandalone
            || _workspaceState.IsCurrentLoaded(
                snapshot.Generation,
                snapshot.TemplateId);
    }

    public FlowParam? ActiveFlowParam
    {
        get
        {
            if (_view.IsStandalone)
                return _view.GetStandaloneExecutionTemplate()?.Value;
            lock (_selectionSync)
                return _loadedFlowParam;
        }
    }

    /// <summary>
    /// Flow selected by the user, even while its canvas is still loading.
    /// Commands that operate on the current canvas must use ActiveFlowParam;
    /// history queries use this value so selection and analysis stay aligned.
    /// </summary>
    public FlowParam? RequestedFlowParam
    {
        get
        {
            if (_view.IsStandalone)
                return _view.GetStandaloneExecutionTemplate()?.Value;
            return GetRequestedFlowTemplate()?.Value
                ?? ActiveFlowParam;
        }
    }

    public int? RequestedTemplateId
    {
        get
        {
            lock (_selectionSync)
                return _requestedTemplate?.Id;
        }
    }

    public string[] RefreshStartNodeSelection(
        string? selectedName = null)
    {
        string[] startNodeNames =
            _view.FlowEngineControl.GetStartNodeNames();
        _startNodeName =
            FlowTemplateSelectionRules.ResolveStartNodeName(
                startNodeNames,
                selectedName,
                _startNodeName);
        return startNodeNames;
    }

    public string? SelectedStartNodeName => _startNodeName;

    public void SelectStartNode(string? startNodeName)
    {
        _startNodeName = startNodeName;
    }

    public void Dispose()
    {
        _disposed = true;
        CancelPendingRefresh();
        _workspaceState.Dispose();
        GC.SuppressFinalize(this);
    }

    private long SetSelectedFlowTemplate(
        TemplateModel<FlowParam> flowTemplate)
    {
        long generation = _refreshGate.Advance();
        lock (_selectionSync)
        {
            _requestedTemplate = flowTemplate;
            _workspaceState.BeginRequest(
                generation,
                flowTemplate.Id);
        }
        int selectedIndex =
            FlowTemplateSelectionRules.ResolveTemplateIndex(
                TemplateFlow.Params.Select(item => item.Id),
                flowTemplate.Id);
        if (!_view.IsStandalone)
        {
            _flowEngineManager.TemplateFlowParamsIndex = selectedIndex;
            FlowEngineConfig.Instance.LastSelectFlow = flowTemplate.Id;
        }
        return generation;
    }

    private async Task DebouncedRefreshAsync(long generation)
    {
        _refreshCts?.Cancel();
        var cts = new CancellationTokenSource();
        _refreshCts = cts;
        try
        {
            await Task.Delay(200, cts.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }
        if (!_disposed)
            await RefreshAsync(generation, allowEmptyFlow: false);
    }

    private void CancelPendingRefresh()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = null;
    }

    private void ClearDisplayedFlow(FlowParam? flowParam)
    {
        foreach (CVCommonNode node in
            _view.STNodeEditorMain.Nodes
                .OfType<CVCommonNode>())
        {
            _unsubscribeNodeEvents(node);
        }

        _resetNodeTitleProgress();
        _view.FlowEngineControl.LoadFromBase64(string.Empty);
        _view.EditorCanvas.ResetCanvasInteractionMode();
        _view.SetDocumentBaseline(flowParam);
        RefreshStartNodeSelection();
        _workspaceServerNodes.Clear();
        if (!_view.IsStandalone)
            _flowEngineManager.CVBaseServerNodes.Clear();
        lock (_selectionSync)
            _loadedFlowParam = flowParam;
        if (!_view.IsStandalone)
        {
            _flowEngineManager.SelectedFlowParam = flowParam;
            if (flowParam == null)
                _flowEngineManager.TemplateFlowParamsIndex = -1;
        }
        _view.STNodeEditorMain.Invalidate();
        _flowEngineManager.Copilot.PublishContext();
    }

    private void RestorePreviousCanvas(byte[] previousCanvasData)
    {
        try
        {
            byte[] currentCanvasData =
                _view.STNodeEditorMain.GetCanvasData();
            if (currentCanvasData.SequenceEqual(
                    previousCanvasData))
            {
                return;
            }

            foreach (CVCommonNode node in
                _view.STNodeEditorMain.Nodes
                    .OfType<CVCommonNode>())
            {
                _unsubscribeNodeEvents(node);
            }

            _view.FlowEngineControl.LoadFromBase64(
                Convert.ToBase64String(previousCanvasData));
            FlowParam? loadedFlowParam;
            lock (_selectionSync)
                loadedFlowParam = _loadedFlowParam;
            _view.SetDocumentBaseline(loadedFlowParam);
            RefreshStartNodeSelection();

            IList<CVBaseServerNode> serverNodes =
                _view.IsStandalone
                    ? _workspaceServerNodes
                    : _flowEngineManager.CVBaseServerNodes;
            serverNodes.Clear();
            foreach (CVCommonNode node in
                _view.STNodeEditorMain.Nodes
                    .OfType<CVCommonNode>())
            {
                if (node is CVBaseServerNode serverNode)
                    serverNodes.Insert(0, serverNode);
                _subscribeNodeEvents(node);
            }
            _view.STNodeEditorMain.Invalidate();
        }
        catch (Exception restoreException)
        {
            log.Error(
                "恢复流程加载失败前的画布失败。",
                restoreException);
        }
    }

    private void RestoreSelectionAfterFailedRefresh(long generation)
    {
        FlowParam? loadedFlowParam;
        TemplateModel<FlowParam>? loadedTemplate;
        lock (_selectionSync)
        {
            loadedFlowParam = _loadedFlowParam;
            loadedTemplate = loadedFlowParam == null
                ? null
                : TemplateFlow.Params.FirstOrDefault(
                    item => item.Id == loadedFlowParam.Id)
                    ?? new TemplateModel<FlowParam>(
                        loadedFlowParam.Name,
                        loadedFlowParam);
            _requestedTemplate = loadedTemplate;
            _workspaceState.TryCompleteFailed(
                generation,
                "流程模板加载失败，已保留原画布。请刷新后重试。");
        }

        if (_view.IsStandalone)
            return;

        _flowEngineManager.SelectedFlowParam = loadedFlowParam;
        int selectedIndex = loadedTemplate == null
            ? -1
            : FlowTemplateSelectionRules.ResolveTemplateIndex(
                TemplateFlow.Params.Select(item => item.Id),
                loadedTemplate.Id);
        _flowEngineManager.TemplateFlowParamsIndex =
            selectedIndex;
        if (loadedTemplate != null)
        {
            FlowEngineConfig.Instance.LastSelectFlow =
                loadedTemplate.Id;
        }
    }
}

internal enum FlowTemplateWorkspaceStatus
{
    Empty,
    Pending,
    Loading,
    Loaded,
    Failed,
    Superseded,
    Disposed,
}

internal readonly record struct FlowTemplateWorkspaceSettlement(
    long Generation,
    FlowTemplateWorkspaceStatus Status,
    int? TemplateId,
    string? FailureReason);

internal sealed class FlowTemplateWorkspaceState : IDisposable
{
    private readonly object _sync = new();
    private long _generation;
    private FlowTemplateWorkspaceStatus _status =
        FlowTemplateWorkspaceStatus.Empty;
    private int? _templateId;
    private TaskCompletionSource<FlowTemplateWorkspaceSettlement>
        _settlement = CreateCompletedEmpty();
    private bool _disposed;

    public void BeginRequest(long generation, int? templateId)
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            _settlement.TrySetResult(
                new FlowTemplateWorkspaceSettlement(
                    _generation,
                    FlowTemplateWorkspaceStatus.Superseded,
                    _templateId,
                    null));
            _generation = generation;
            _templateId = templateId;
            _status = FlowTemplateWorkspaceStatus.Pending;
            _settlement =
                new TaskCompletionSource<
                    FlowTemplateWorkspaceSettlement>(
                    TaskCreationOptions
                        .RunContinuationsAsynchronously);
        }
    }

    public bool TryMarkLoading(long generation)
    {
        lock (_sync)
        {
            if (_disposed || generation != _generation)
                return false;
            _status = FlowTemplateWorkspaceStatus.Loading;
            return true;
        }
    }

    public bool TryCompleteLoaded(
        long generation,
        int templateId)
    {
        return TryComplete(
            generation,
            FlowTemplateWorkspaceStatus.Loaded,
            templateId,
            null);
    }

    public bool TryCompleteEmpty(long generation)
    {
        return TryComplete(
            generation,
            FlowTemplateWorkspaceStatus.Empty,
            null,
            null);
    }

    public bool TryCompleteFailed(
        long generation,
        string failureReason)
    {
        return TryComplete(
            generation,
            FlowTemplateWorkspaceStatus.Failed,
            _templateId,
            failureReason);
    }

    public async Task<FlowTemplateWorkspaceSettlement>
        WaitForCurrentSettlementAsync()
    {
        while (true)
        {
            Task<FlowTemplateWorkspaceSettlement> waitTask;
            long generation;
            lock (_sync)
            {
                generation = _generation;
                waitTask = _settlement.Task;
            }

            FlowTemplateWorkspaceSettlement settlement =
                await waitTask;
            lock (_sync)
            {
                if (generation == _generation
                    && settlement.Generation == _generation)
                {
                    return settlement;
                }
            }
        }
    }

    public bool IsCurrentLoaded(
        long generation,
        int? templateId)
    {
        lock (_sync)
        {
            return !_disposed
                && _generation == generation
                && _status == FlowTemplateWorkspaceStatus.Loaded
                && _templateId == templateId;
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
            _status = FlowTemplateWorkspaceStatus.Disposed;
            var disposedSettlement =
                new FlowTemplateWorkspaceSettlement(
                    _generation,
                    _status,
                    _templateId,
                    "流程工作区已经关闭。");
            _settlement.TrySetResult(disposedSettlement);
            _settlement =
                new TaskCompletionSource<
                    FlowTemplateWorkspaceSettlement>(
                    TaskCreationOptions
                        .RunContinuationsAsynchronously);
            _settlement.SetResult(disposedSettlement);
        }
    }

    private bool TryComplete(
        long generation,
        FlowTemplateWorkspaceStatus status,
        int? templateId,
        string? failureReason)
    {
        lock (_sync)
        {
            if (_disposed || generation != _generation)
                return false;
            _status = status;
            _templateId = templateId;
            _settlement.TrySetResult(
                new FlowTemplateWorkspaceSettlement(
                    generation,
                    status,
                    templateId,
                    failureReason));
            return true;
        }
    }

    private static TaskCompletionSource<
        FlowTemplateWorkspaceSettlement>
        CreateCompletedEmpty()
    {
        var completion =
            new TaskCompletionSource<
                FlowTemplateWorkspaceSettlement>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);
        completion.SetResult(
            new FlowTemplateWorkspaceSettlement(
                0,
                FlowTemplateWorkspaceStatus.Empty,
                null,
                null));
        return completion;
    }
}

internal sealed record FlowTemplateExecutionSnapshot(
    long Generation,
    int TemplateId,
    string FlowName,
    string DataBase64,
    int? ResourceId,
    string? ResourceCode,
    string? FlowKey,
    int? TemplateRevision,
    string? TemplateContentHash,
    string? LoadedContentHash)
{
    public static FlowTemplateExecutionSnapshot Create(
        long generation,
        FlowParam flowParam)
    {
        ArgumentNullException.ThrowIfNull(flowParam);
        return new FlowTemplateExecutionSnapshot(
            generation,
            flowParam.Id,
            flowParam.Name,
            flowParam.DataBase64,
            flowParam.ResourceId,
            flowParam.ResourceCode,
            flowParam.FlowKey,
            flowParam.TemplateRevision,
            flowParam.TemplateContentHash,
            flowParam.LoadedContentHash);
    }

    public FlowParam CreateFlowParam()
    {
        return new FlowParam
        {
            Id = TemplateId,
            Name = FlowName,
            DataBase64 = DataBase64,
            ResourceId = ResourceId,
            ResourceCode = ResourceCode,
            FlowKey = FlowKey,
            TemplateRevision = TemplateRevision,
            TemplateContentHash = TemplateContentHash,
            LoadedContentHash = LoadedContentHash,
        };
    }
}

internal readonly record struct
    FlowTemplateExecutionSnapshotResult(
        FlowTemplateExecutionSnapshot? Snapshot,
        string? FailureReason)
{
    public static FlowTemplateExecutionSnapshotResult Loaded(
        FlowTemplateExecutionSnapshot snapshot)
    {
        return new FlowTemplateExecutionSnapshotResult(
            snapshot,
            null);
    }

    public static FlowTemplateExecutionSnapshotResult Failed(
        string failureReason)
    {
        return new FlowTemplateExecutionSnapshotResult(
            null,
            failureReason);
    }
}

internal sealed class FlowTemplateRefreshGate
{
    private readonly SemaphoreSlim _serialGate = new(1, 1);
    private long _generation;
    private int _isRefreshing;

    public bool IsRefreshing => Volatile.Read(ref _isRefreshing) != 0;

    public long Advance()
    {
        return Interlocked.Increment(ref _generation);
    }

    public bool IsCurrent(long generation)
    {
        return generation == Volatile.Read(ref _generation);
    }

    public async Task ExecuteLatestAsync(
        long generation,
        Func<Func<bool>, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        await _serialGate.WaitAsync();
        try
        {
            if (!IsCurrent(generation))
                return;

            Interlocked.Exchange(ref _isRefreshing, 1);
            await action(() => IsCurrent(generation));
        }
        finally
        {
            Interlocked.Exchange(ref _isRefreshing, 0);
            _serialGate.Release();
        }
    }

    public async Task WaitUntilIdleAsync()
    {
        await _serialGate.WaitAsync();
        _serialGate.Release();
    }
}

internal static class FlowTemplateSelectionRules
{
    public static int ResolveTemplateIndex(
        IEnumerable<int> templateIds,
        int selectedTemplateId)
    {
        int index = 0;
        foreach (int templateId in templateIds)
        {
            if (templateId == selectedTemplateId)
                return index;
            index++;
        }
        return -1;
    }

    public static string? ResolveStartNodeName(
        IReadOnlyCollection<string> availableNames,
        string? requestedName,
        string? currentName)
    {
        if (!string.IsNullOrWhiteSpace(requestedName)
            && availableNames.Contains(requestedName))
        {
            return requestedName;
        }

        if (!string.IsNullOrWhiteSpace(currentName)
            && availableNames.Contains(currentName))
        {
            return currentName;
        }

        return availableNames.FirstOrDefault();
    }
}
