using System.Windows.Threading;

namespace ColorVision.Solution.MultiImageViewer;

public partial class MultiImageViewer
{
    private static readonly int ThumbnailWorkerLimit = Math.Max(1, Environment.ProcessorCount / 2);
    private readonly HashSet<ImageFileInfo> _realizedThumbnailRows = new();
    private readonly HashSet<ImageFileInfo> _pendingThumbnailRows = new();
    private readonly Dictionary<ImageFileInfo, CancellationTokenSource> _thumbnailLoads = new();
    private (bool Enabled, int Size, bool UseCache) _thumbnailSettings;
    private bool _thumbnailPumpScheduled;

    private void ObserveThumbnailViewport()
    {
        FileListBox.ItemRealized += file =>
        {
            if (_disposed || !_realizedThumbnailRows.Add(file)) return;
            RequestThumbnail(file);
        };
        FileListBox.ItemUnrealized += file =>
        {
            if (!_realizedThumbnailRows.Remove(file)) return;
            ReleaseThumbnail(file);
        };
        FileListBox.Loaded += (_, _) => ResumeThumbnailRows();
        FileListBox.Unloaded += (_, _) => ReleaseThumbnailRows();
        FileListBox.IsVisibleChanged += (_, _) =>
        {
            if (FileListBox.IsVisible) ResumeThumbnailRows();
            else ReleaseThumbnailRows();
        };
    }

    private bool CanLoadThumbnails => !_disposed && _thumbnailSettings.Enabled && FileListBox.IsLoaded && FileListBox.IsVisible;

    private void ResumeThumbnailRows()
    {
        foreach (ImageFileInfo file in _realizedThumbnailRows) RequestThumbnail(file);
    }

    private void RequestThumbnail(ImageFileInfo file)
    {
        if (!CanLoadThumbnails || !file.FileExists || file.Thumbnail != null) return;
        if (_thumbnailLoads.TryGetValue(file, out CancellationTokenSource? load) && !load.IsCancellationRequested) return;
        if (_pendingThumbnailRows.Add(file)) ScheduleThumbnailPump();
    }

    private void ReleaseThumbnailRows()
    {
        _pendingThumbnailRows.Clear();
        foreach (ImageFileInfo file in _realizedThumbnailRows.ToArray()) ReleaseThumbnail(file);
    }

    private void ResetThumbnailRows()
    {
        ImageFileInfo[] rows = _realizedThumbnailRows.ToArray();
        _realizedThumbnailRows.Clear();
        _pendingThumbnailRows.Clear();
        foreach (ImageFileInfo file in rows) ReleaseThumbnail(file);
    }

    private void ReleaseThumbnail(ImageFileInfo file)
    {
        _pendingThumbnailRows.Remove(file);
        if (_thumbnailLoads.TryGetValue(file, out CancellationTokenSource? load)) load.Cancel();
        if (file.Thumbnail != null) file.Thumbnail = null;
    }

    private void ScheduleThumbnailPump()
    {
        if (_thumbnailPumpScheduled || !CanLoadThumbnails || _pendingThumbnailRows.Count == 0) return;
        _thumbnailPumpScheduled = true;
        // A realized row can be reported during layout. Start file I/O after layout returns.
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)PumpThumbnails);
    }

    private void PumpThumbnails()
    {
        _thumbnailPumpScheduled = false;
        if (!CanLoadThumbnails) return;
        foreach (ImageFileInfo file in _pendingThumbnailRows.ToArray())
        {
            if (_thumbnailLoads.Count >= ThumbnailWorkerLimit) return;
            // A recycled row may have returned while its canceled decoder is still finishing.
            if (_thumbnailLoads.ContainsKey(file) || !_pendingThumbnailRows.Remove(file)) continue;
            var load = new CancellationTokenSource();
            _thumbnailLoads.Add(file, load);
            _ = LoadThumbnailAsync(file, load, _thumbnailSettings.Size, _thumbnailSettings.UseCache);
        }
    }

    private async Task LoadThumbnailAsync(ImageFileInfo file, CancellationTokenSource load, int size, bool useCache)
    {
        try
        {
            await file.LoadThumbnailAsync(size, useCache, load.Token);
        }
        catch (OperationCanceledException) when (load.IsCancellationRequested) { }
        catch (Exception error)
        {
            System.Diagnostics.Debug.WriteLine($"Thumbnail load failed: {error.Message}");
        }
        finally
        {
            _thumbnailLoads.Remove(file);
            load.Dispose();
            ScheduleThumbnailPump();
        }
    }
}
