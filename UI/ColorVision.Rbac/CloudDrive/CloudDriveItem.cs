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

    private double _progress;
    [JsonIgnore] public double Progress { get => _progress; set => SetProperty(ref _progress, value); }
    [JsonIgnore] public bool IsComplete => !string.IsNullOrEmpty(ShareUrl);
    [JsonIgnore] public bool CanShare => IsComplete && (!ExpiresAt.HasValue || ExpiresAt > DateTimeOffset.Now);
    [JsonIgnore] public string SizeText => FormatSize(Size);
    [JsonIgnore] public string Detail => !string.IsNullOrEmpty(Error) ? Error : IsComplete
        ? ExpiresAt.HasValue ? $"分享有效至 {ExpiresAt.Value.LocalDateTime:yyyy-MM-dd HH:mm}" : "上传完成，可分享"
        : $"{FormatSize(Offset)} / {SizeText}";

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
