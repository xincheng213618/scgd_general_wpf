using System.ComponentModel;
using System.IO;

namespace CameraTest.Models;

public sealed record ArchiveSettings
{
    [Category("设备"), DisplayName("生产设备编号 / SN"), Description("存档必填，与 SDK 连接 ID 分开记录。")]
    public string DeviceSerial { get; set; } = "";
    [Category("记录"), DisplayName("操作员")]
    public string Operator { get; set; } = "";
    [Category("记录"), DisplayName("批次 / 工单")]
    public string Batch { get; set; } = "";
    [Category("记录"), DisplayName("备注")]
    public string Notes { get; set; } = "";
    [Category("存储"), DisplayName("本地存档目录")]
    public string RootDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ColorVision", "CameraTest", "Archive");

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DeviceSerial)) throw new ArgumentException("请填写本次调试设备的生产编号 / SN。");
        if (string.IsNullOrWhiteSpace(RootDirectory) || !Path.IsPathFullyQualified(RootDirectory)) throw new ArgumentException("请选择完整的本地存档目录路径。");
        if (DeviceSerial.Length > 256 || Operator.Length > 256 || Batch.Length > 256 || Notes.Length > 4000) throw new ArgumentException("存档信息过长，请缩短设备编号、批次或备注。");
    }
}
