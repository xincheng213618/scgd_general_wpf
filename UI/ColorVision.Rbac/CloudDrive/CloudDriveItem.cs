using ColorVision.Common.MVVM;
using System.Text.Json.Serialization;

namespace ColorVision.Rbac.CloudDrive;

public sealed class CloudDriveItem : ViewModelBase
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SourcePath { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsFolder { get; set; }
    public string UploadPath { get; set; } = "";
    public string RemoteName { get; set; } = "";
    public long Size { get; set; }
    public long LastWriteTicks { get; set; }
    public string Fingerprint { get; set; } = "";
    public string UploadId { get; set; } = "";
    public long Offset { get; set; }
    public string ShareUrl { get; set; } = "";
    public DateTimeOffset? ExpiresAt { get; set; }
    public string Status { get; set; } = "等待上传";
    public string Error { get; set; } = "";

    internal long? SentBytes { get; set; }
    internal double? UploadBytesPerSecond { get; set; }
    internal TimeSpan? EstimatedRemaining { get; set; }
    internal string TransferNotice { get; set; } = "";

    [JsonIgnore] public double Progress
    {
        get
        {
            long displayedBytes = SentBytes ?? Offset;
            if (IsComplete || (Size > 0 && displayedBytes >= Size)) return 100;
            return Size > 0 ? Math.Min(99.9, 100d * displayedBytes / Size) : 0;
        }
    }

    [JsonIgnore] public bool HasUploadProgress => SentBytes.HasValue && !IsComplete;
    [JsonIgnore] public string ProgressText => $"{Progress:0.0}%";
    [JsonIgnore] public bool IsComplete => !string.IsNullOrEmpty(ShareUrl);
    [JsonIgnore] public bool CanShare => IsComplete && (!ExpiresAt.HasValue || ExpiresAt > DateTimeOffset.Now);
    [JsonIgnore] public string SizeText => FormatSize(Size);
    [JsonIgnore] public string Detail
    {
        get
        {
            if (IsComplete)
                return ExpiresAt.HasValue ? $"分享有效至 {ExpiresAt.Value.LocalDateTime:yyyy-MM-dd HH:mm}" : "上传完成，可分享";
            if (SentBytes is long sent)
                return $"已发送 {FormatSize(sent)} / {SizeText} · 服务端已确认 {FormatSize(Offset)}";
            return Offset > 0 || !string.IsNullOrEmpty(UploadId) ? $"服务端已确认 {FormatSize(Offset)} / {SizeText}" : SizeText;
        }
    }
    [JsonIgnore] public string UploadMessage => string.IsNullOrEmpty(Error) ? TransferNotice : Error;
    [JsonIgnore] public bool HasUploadMessage => !string.IsNullOrEmpty(UploadMessage);
    [JsonIgnore] public string CurrentSpeedText => UploadBytesPerSecond is double speed
        ? $"当前速度 {FormatSize((long)Math.Round(speed))}/s"
        : "正在测速…";
    [JsonIgnore] public string RemainingTimeText => HasUploadProgress && SentBytes < Size && !HasUploadMessage
        ? EstimatedRemaining is TimeSpan remaining ? $"预计剩余 {FormatRemainingTime(remaining)}" : "正在估算剩余时间…"
        : "";

    private static string FormatRemainingTime(TimeSpan remaining) => remaining switch
    {
        { TotalHours: >= 1 } => $"{(long)remaining.TotalHours} 小时 {remaining.Minutes} 分",
        { TotalMinutes: >= 1 } => $"{remaining.Minutes} 分 {remaining.Seconds} 秒",
        _ => $"{remaining.Seconds} 秒"
    };

    public void Refresh()
    {
        OnPropertyChanged(string.Empty);
    }

    public static string FormatSize(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / (1024d * 1024 * 1024):0.##} GB",
        >= 1024 * 1024 => $"{bytes / (1024d * 1024):0.##} MB",
        >= 1024 => $"{bytes / 1024d:0.##} KB",
        _ => $"{bytes} B"
    };
}

public sealed class CloudDriveState
{
    public int Version { get; set; } = 1;
    public string ClientId { get; set; } = Guid.NewGuid().ToString();
    public string ServiceUrl { get; set; } = "";
    public List<CloudDriveItem> Items { get; set; } = [];
}
