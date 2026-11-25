using ColorVision.Common.MVVM;
using ColorVision.UI.Marketplace;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows.Threading;
using log4net;

namespace ColorVision.Rbac.CloudDrive;

public sealed class CloudDriveManager : ViewModelBase
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(CloudDriveManager));
    private static CloudDriveManager? _instance;
    public static CloudDriveManager Instance => _instance ??= new CloudDriveManager();
    private readonly CloudDriveStore _store;
    private readonly CloudDriveState _state;
    private readonly TransferClient _client;
    private CancellationTokenSource? _cancellation;
    public ObservableCollection<CloudDriveItem> Items { get; }
    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set { SetProperty(ref _isBusy, value); OnPropertyChanged(nameof(IsIdle)); } }
    public bool IsIdle => !IsBusy;
    private string _status = "选择文件或文件夹，也可以拖放到这里。文件夹将打包为 ZIP。";
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    private string _limitText = "免账号分享 · 分享有效期 24 小时 · 正在读取服务器限制…";
    public string LimitText { get => _limitText; private set => SetProperty(ref _limitText, value); }

    private CloudDriveManager() : this(
        new CloudDriveStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ColorVision", "CloudDrive")),
        new Uri(MarketplaceConfig.ServiceBaseUrl.TrimEnd('/') + "/"),
        new HttpClient(new HttpClientHandler { UseCookies = false }) { Timeout = TimeSpan.FromMinutes(5) })
    { }

    public CloudDriveManager(CloudDriveStore store, Uri serviceUri, HttpClient http)
    {
        _store = store;
        _state = _store.Load(serviceUri.AbsoluteUri);
        Items = new ObservableCollection<CloudDriveItem>(_state.Items);
        // The cloud drive always uses its own anonymous identity, independent of RBAC and browser login.
        _client = new TransferClient(http, serviceUri, _state.ClientId);
        Save();
    }

    private void Save()
    {
        _state.Items = Items.ToList();
        _store.Save(_state);
    }

    public async Task RefreshCapabilitiesAsync()
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var capabilities = await _client.GetCapabilitiesAsync(timeout.Token);
            SetCapabilities(capabilities);
        }
        catch (Exception ex) { LimitText = "无法读取服务器限制：" + ex.Message; }
    }

    private void SetCapabilities(TransferCapabilities capabilities)
    {
        LimitText = capabilities.AnonymousTransferUploadEnabled
            ? $"无需账号 · 单文件 / 压缩包上限 {CloudDriveItem.FormatSize(capabilities.AnonymousTransferMaxBytes)} · 分享有效期 24 小时"
            : "服务器尚未开放免账号上传，需启用匿名文件中转。";
    }

    public void AddPaths(IEnumerable<string> paths)
    {
        if (IsBusy) return;
        foreach (string path in paths)
        {
            bool folder = Directory.Exists(path);
            if (!folder && !File.Exists(path)) continue;
            string fullPath = Path.GetFullPath(path);
            if (Items.Any(item => !item.IsComplete && string.Equals(item.SourcePath, fullPath, StringComparison.OrdinalIgnoreCase))) continue;
            string displayName = folder ? new DirectoryInfo(fullPath).Name + ".zip" : Path.GetFileName(fullPath);
            var entry = new CloudDriveItem
            {
                SourcePath = fullPath, DisplayName = displayName, IsFolder = folder,
                RemoteName = TransferClient.UniqueRemoteName(displayName), Size = folder ? 0 : new FileInfo(fullPath).Length
            };
            entry.UploadPath = folder ? Path.Combine(_store.PackagesDirectory, entry.Id + ".zip") : fullPath;
            Items.Add(entry);
        }
        Save();
        Status = "已加入队列，点击开始 / 继续上传。";
    }

    public void Remove(CloudDriveItem item)
    {
        if (IsBusy) return;
        Items.Remove(item);
        Save();
        DeleteOwnedPackage(item);
        Status = "已移除本机记录；已发出的分享链接仍在原有效期内可用。";
    }

    private void DeleteOwnedPackage(CloudDriveItem item)
    {
        if (!item.IsFolder) return;
        string expected = Path.GetFullPath(Path.Combine(_store.PackagesDirectory, item.Id + ".zip"));
        if (!string.Equals(expected, Path.GetFullPath(item.UploadPath), StringComparison.OrdinalIgnoreCase)) return;
        if (File.Exists(expected)) File.Delete(expected);
    }

    public void Pause() => _cancellation?.Cancel();

    public async Task UploadAsync(CloudDriveItem? selected = null)
    {
        if (IsBusy) return;
        var pending = Items.Where(item => !item.IsComplete && (selected == null || item == selected)).ToList();
        if (pending.Count == 0) { Status = "请先添加要上传的文件或文件夹。"; return; }
        IsBusy = true;
        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        try
        {
            Status = "正在连接云盘服务器…";
            var capabilities = await TransferClient.RetryRequestAsync(() => _client.GetCapabilitiesAsync(cancellation.Token), message => Status = message, cancellation.Token);
            SetCapabilities(capabilities);
            if (!capabilities.AnonymousTransferUploadEnabled) throw new IOException(LimitText);
            foreach (var item in pending)
            {
                if (cancellation.IsCancellationRequested) break;
                bool showProgress = true;
                DispatcherTimer? speedTimer = null;
                try
                {
                    item.Error = "";
                    if (item.IsFolder && !File.Exists(item.UploadPath))
                    {
                        if (item.Fingerprint.Length > 0) throw new IOException("续传所需的本地 ZIP 已丢失，请移除此记录后重新添加文件夹。");
                        item.Status = "正在打包";
                        item.Refresh();
                        Status = $"正在打包 {item.DisplayName}，完成后开始上传。";
                        var packageProgress = new Progress<long>(bytes => { Status = $"正在打包 {item.DisplayName} · 已读取 {CloudDriveItem.FormatSize(bytes)}"; });
                        await Task.Run(() => CloudDrivePackage.CreateAsync(item.SourcePath, item.UploadPath, capabilities.AnonymousTransferMaxBytes, packageProgress, cancellation.Token), cancellation.Token);
                    }
                    item.Size = new FileInfo(item.UploadPath).Length;
                    if (item.Size > capabilities.AnonymousTransferMaxBytes) throw new IOException($"超过服务器上限 {CloudDriveItem.FormatSize(capabilities.AnonymousTransferMaxBytes)}，请分批上传。");
                    item.Status = "校验文件 / 查询断点";
                    item.Refresh();
                    Status = $"正在处理 {item.DisplayName}。可最小化窗口继续使用软件。";
                    var transferTime = new Stopwatch();
                    var lastActivity = new Stopwatch();
                    long resumeOffset = 0;
                    long sampledBytes = 0;
                    double sampledAt = 0;
                    string? retryNotice = null;
                    var progress = new Progress<TransferProgress>(value =>
                    {
                        if (!showProgress || item.IsComplete) return;
                        if (!transferTime.IsRunning || retryNotice != null || value.Sent < item.SentBytes)
                        {
                            resumeOffset = value.Confirmed;
                            sampledBytes = value.Confirmed;
                            sampledAt = 0;
                            transferTime.Restart();
                            item.UploadBytesPerSecond = null;
                        }
                        lastActivity.Restart();
                        retryNotice = value.RetryNotice;
                        item.TransferNotice = retryNotice ?? "";
                        item.SentBytes = value.Sent;
                        if (retryNotice != null) item.UploadBytesPerSecond = 0;
                        long sentThisRun = value.Sent - resumeOffset;
                        double elapsedSeconds = transferTime.Elapsed.TotalSeconds;
                        item.EstimatedRemaining = retryNotice == null && value.Sent < value.Total && sentThisRun > 0 && elapsedSeconds >= 0.5
                            ? TimeSpan.FromSeconds(Math.Ceiling((value.Total - value.Sent) * elapsedSeconds / sentThisRun))
                            : null;
                        item.Status = retryNotice != null ? "等待自动重试" : value.Sent == value.Total ? "服务器确认中"
                            : value.AwaitingConfirmation ? "等待分块确认" : "正在上传";
                        item.Refresh();
                    });
                    speedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                    speedTimer.Tick += (_, _) =>
                    {
                        if (!showProgress || item.IsComplete || item.SentBytes is not long sent || retryNotice != null) return;
                        double elapsed = transferTime.Elapsed.TotalSeconds;
                        double sampleDuration = elapsed - sampledAt;
                        if (sampleDuration < 0.5) return;
                        item.UploadBytesPerSecond = Math.Max(0, sent - sampledBytes) / sampleDuration;
                        sampledAt = elapsed;
                        sampledBytes = sent;
                        if (lastActivity.Elapsed.TotalSeconds >= 5)
                        {
                            item.TransferNotice = item.Status is "服务器确认中" or "等待分块确认"
                                ? "等待服务器确认，已确认的数据可用于续传。"
                                : "暂时没有发送进展，连接超时后将自动重试。";
                            item.EstimatedRemaining = null;
                        }
                        item.Refresh();
                    };
                    speedTimer.Start();
                    await _client.UploadAsync(item, () => { Save(); item.Refresh(); }, progress, cancellation.Token);
                    item.Status = item.CanShare ? "上传完成" : "分享已过期";
                    Save();
                    // Only remove the ZIP created by this item, after persisting the completed receipt.
                    try { DeleteOwnedPackage(item); } catch (IOException) { }
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    item.Status = "已暂停，可继续";
                    item.Error = "断点已保留；继续上传时将查询服务器确认位置。";
                }
                catch (Exception ex)
                {
                    item.Status = item.IsComplete ? "上传完成" : "上传失败，可重试";
                    item.Error = ex.Message;
                    Log.Warn($"云盘上传失败（服务器已确认 {item.Offset} / {item.Size} 字节）：{ex.Message}");
                }
                finally
                {
                    showProgress = false;
                    speedTimer?.Stop();
                    item.SentBytes = null;
                    item.UploadBytesPerSecond = null;
                    item.EstimatedRemaining = null;
                    item.TransferNotice = "";
                }
                item.Refresh();
                Save();
            }
            int done = pending.Count(item => item.IsComplete);
            Status = cancellation.IsCancellationRequested ? "已暂停，点击继续上传可恢复；退出软件也会保留记录。"
                : $"本次完成 {done} / {pending.Count} 项。选择已完成的文件可复制链接或查看二维码。";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { Status = "已暂停，上传记录已保留。"; }
        catch (Exception ex) { Status = ex.Message; }
        finally
        {
            _cancellation = null;
            IsBusy = false;
        }
    }
}
