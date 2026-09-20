using System.IO;
using System.Text.Json;

namespace ColorVision.Rbac.CloudDrive;

public sealed class CloudDriveStore(string directory)
{
    private readonly string _directory = Path.GetFullPath(directory);
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public string PackagesDirectory => Path.Combine(_directory, "Packages");

    public CloudDriveState Load(string serviceUrl)
    {
        string path = Path.Combine(_directory, "transfers.json");
        if (!File.Exists(path)) return new CloudDriveState { ServiceUrl = serviceUrl };
        var state = JsonSerializer.Deserialize<CloudDriveState>(File.ReadAllText(path))
            ?? throw new InvalidDataException("云盘记录为空，请保留记录文件并检查磁盘。");
        if (state.Version != 1 || !Guid.TryParse(state.ClientId, out _) || state.Items == null ||
            !string.Equals(state.ServiceUrl, serviceUrl, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("云盘记录版本、客户端标识或服务器地址不匹配，原记录已保留。");
        foreach (var item in state.Items)
        {
            if (!Guid.TryParseExact(item.Id, "N", out _)) throw new InvalidDataException("云盘记录标识无效。");
            item.Progress = item.IsComplete ? 100 : item.Size > 0 ? 100d * item.Offset / item.Size : 0;
            item.Status = item.IsComplete ? item.CanShare ? "上传完成" : "分享已过期" : "已暂停，可继续";
        }
        return state;
    }

    public void Save(CloudDriveState state)
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "transfers.json");
        string temporary = path + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, state, Options);
            stream.Flush(flushToDisk: true);
        }
        File.Move(temporary, path, overwrite: true);
    }
}
