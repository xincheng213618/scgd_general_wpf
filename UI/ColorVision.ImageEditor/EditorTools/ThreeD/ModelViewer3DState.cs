using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ImageEditor.EditorTools.ThreeD
{
    internal enum ModelViewerRenderMode
    {
        Textured,
        Solid,
        Wireframe,
    }

    internal enum ModelViewerProjection
    {
        Perspective,
        Orthographic,
    }

    internal enum ModelViewerLoadState
    {
        Empty,
        Loading,
        Ready,
        Error,
    }

    internal enum ModelLoadOperationStatus
    {
        Succeeded,
        Canceled,
        Superseded,
        Failed,
    }

    internal sealed record ModelViewerDefaults(ModelViewerRenderMode RenderMode, ModelViewerProjection Projection);

    internal sealed class ModelViewer3DSession : INotifyPropertyChanged
    {
        public ModelViewer3DSession(ModelViewerDefaults defaults)
        {
            RenderMode = defaults.RenderMode;
            Projection = defaults.Projection;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string? CurrentPath
        {
            get => currentPath;
            private set => SetProperty(ref currentPath, value);
        }
        private string? currentPath;

        public string? PendingPath
        {
            get => pendingPath;
            private set => SetProperty(ref pendingPath, value);
        }
        private string? pendingPath;

        public ModelViewerLoadState LoadState
        {
            get => loadState;
            private set => SetProperty(ref loadState, value);
        }
        private ModelViewerLoadState loadState = ModelViewerLoadState.Empty;

        public ModelViewerRenderMode RenderMode
        {
            get => renderMode;
            set => SetProperty(ref renderMode, value);
        }
        private ModelViewerRenderMode renderMode;

        public ModelViewerProjection Projection
        {
            get => projection;
            set => SetProperty(ref projection, value);
        }
        private ModelViewerProjection projection;

        public Guid? SelectedNodeId
        {
            get => selectedNodeId;
            set => SetProperty(ref selectedNodeId, value);
        }
        private Guid? selectedNodeId;

        public string? ErrorMessage
        {
            get => errorMessage;
            private set => SetProperty(ref errorMessage, value);
        }
        private string? errorMessage;

        public void BeginLoad(string path)
        {
            PendingPath = path;
            ErrorMessage = null;
            LoadState = ModelViewerLoadState.Loading;
        }

        public void CompleteLoad(string path)
        {
            CurrentPath = path;
            PendingPath = null;
            ErrorMessage = null;
            SelectedNodeId = null;
            LoadState = ModelViewerLoadState.Ready;
        }

        public void CancelLoad()
        {
            PendingPath = null;
            LoadState = CurrentPath == null ? ModelViewerLoadState.Empty : ModelViewerLoadState.Ready;
        }

        public void FailLoad(string message)
        {
            PendingPath = null;
            ErrorMessage = message;
            LoadState = CurrentPath == null ? ModelViewerLoadState.Error : ModelViewerLoadState.Ready;
        }

        private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    internal sealed class SceneVisibilityState
    {
        private readonly HashSet<Guid> hiddenNodeIds = new();
        private HashSet<Guid>? isolatedNodeIds;

        public bool IsIsolated => isolatedNodeIds != null;

        public bool IsVisible(Guid nodeId)
        {
            return isolatedNodeIds?.Contains(nodeId) ?? !hiddenNodeIds.Contains(nodeId);
        }

        public bool IsHidden(Guid nodeId)
        {
            return hiddenNodeIds.Contains(nodeId);
        }

        public void SetHidden(IEnumerable<Guid> nodeIds, bool hidden)
        {
            ArgumentNullException.ThrowIfNull(nodeIds);
            foreach (Guid nodeId in nodeIds)
            {
                if (hidden)
                    hiddenNodeIds.Add(nodeId);
                else
                    hiddenNodeIds.Remove(nodeId);
            }
        }

        public void EnterIsolation(IEnumerable<Guid> visibleNodeIds)
        {
            ArgumentNullException.ThrowIfNull(visibleNodeIds);
            isolatedNodeIds = new HashSet<Guid>(visibleNodeIds);
        }

        public void ExitIsolation()
        {
            isolatedNodeIds = null;
        }

        public void ShowAll()
        {
            hiddenNodeIds.Clear();
            isolatedNodeIds = null;
        }

        public void Reset()
        {
            ShowAll();
        }

        public SceneVisibilityState Clone()
        {
            SceneVisibilityState clone = new();
            clone.hiddenNodeIds.UnionWith(hiddenNodeIds);
            if (isolatedNodeIds != null)
                clone.isolatedNodeIds = new HashSet<Guid>(isolatedNodeIds);
            return clone;
        }

        public void CopyFrom(SceneVisibilityState source)
        {
            ArgumentNullException.ThrowIfNull(source);
            hiddenNodeIds.Clear();
            hiddenNodeIds.UnionWith(source.hiddenNodeIds);
            isolatedNodeIds = source.isolatedNodeIds == null ? null : new HashSet<Guid>(source.isolatedNodeIds);
        }
    }

    internal sealed record ModelLoadOperationResult<T>(ModelLoadOperationStatus Status, T? Value, Exception? Error)
        where T : class, IDisposable
    {
        public static ModelLoadOperationResult<T> Succeeded(T value) => new(ModelLoadOperationStatus.Succeeded, value, null);
        public static ModelLoadOperationResult<T> Canceled() => new(ModelLoadOperationStatus.Canceled, null, null);
        public static ModelLoadOperationResult<T> Superseded() => new(ModelLoadOperationStatus.Superseded, null, null);
        public static ModelLoadOperationResult<T> Failed(Exception error) => new(ModelLoadOperationStatus.Failed, null, error);
    }

    /// <summary>
    /// Serializes model publication without serializing the expensive import itself. A newer request
    /// cancels the previous token and owns the only result that may be published.
    /// </summary>
    internal sealed class LatestModelLoadCoordinator<T> : IDisposable where T : class, IDisposable
    {
        private readonly object sync = new();
        private CancellationTokenSource? activeSource;
        private long generation;
        private bool isDisposed;

        public async Task<ModelLoadOperationResult<T>> RunAsync(Func<CancellationToken, Task<T>> loader)
        {
            ArgumentNullException.ThrowIfNull(loader);

            CancellationTokenSource source;
            CancellationTokenSource? previousSource;
            long requestGeneration;
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(isDisposed, this);
                previousSource = activeSource;
                source = new CancellationTokenSource();
                activeSource = source;
                requestGeneration = ++generation;
            }
            Cancel(previousSource);

            try
            {
                T value = await loader(source.Token).ConfigureAwait(false);
                if (source.IsCancellationRequested)
                {
                    value.Dispose();
                    return IsCurrent(requestGeneration, source)
                        ? ModelLoadOperationResult<T>.Canceled()
                        : ModelLoadOperationResult<T>.Superseded();
                }
                if (!IsCurrent(requestGeneration, source))
                {
                    value.Dispose();
                    return ModelLoadOperationResult<T>.Superseded();
                }

                return ModelLoadOperationResult<T>.Succeeded(value);
            }
            catch (OperationCanceledException) when (source.IsCancellationRequested)
            {
                return IsCurrent(requestGeneration, source)
                    ? ModelLoadOperationResult<T>.Canceled()
                    : ModelLoadOperationResult<T>.Superseded();
            }
            catch (Exception ex)
            {
                return IsCurrent(requestGeneration, source)
                    ? ModelLoadOperationResult<T>.Failed(ex)
                    : ModelLoadOperationResult<T>.Superseded();
            }
            finally
            {
                lock (sync)
                {
                    if (ReferenceEquals(activeSource, source))
                        activeSource = null;
                }
                source.Dispose();
            }
        }

        public void CancelActive()
        {
            CancellationTokenSource? source;
            lock (sync)
                source = activeSource;
            Cancel(source);
        }

        public void Dispose()
        {
            CancellationTokenSource? source;
            lock (sync)
            {
                if (isDisposed)
                    return;

                isDisposed = true;
                generation++;
                source = activeSource;
                activeSource = null;
            }
            Cancel(source);
        }

        private bool IsCurrent(long requestGeneration, CancellationTokenSource source)
        {
            lock (sync)
                return !isDisposed && generation == requestGeneration && ReferenceEquals(activeSource, source);
        }

        private static void Cancel(CancellationTokenSource? source)
        {
            try
            {
                source?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}
