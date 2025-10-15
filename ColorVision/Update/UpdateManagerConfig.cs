using ColorVision.Common.MVVM;

namespace ColorVision.Update;

/// <summary>
/// 更新管理器配置
/// </summary>
public class UpdateManagerConfig : ViewModelBase, IConfig
{
    public static UpdateManagerConfig Instance => ConfigService.Instance.GetRequiredService<UpdateManagerConfig>();

    /// <summary>
    /// 是否使用新的更新机制（双轨并行期间可切换）
    /// </summary>
    public bool UseNewUpdateMechanism { get => _useNewUpdateMechanism; set { _useNewUpdateMechanism = value; OnPropertyChanged(); } }
    private bool _useNewUpdateMechanism = true;

    /// <summary>
    /// 更新器程序路径（默认为程序目录下的 ColorVision.Updater.exe）
    /// </summary>
    public string UpdaterPath { get => _updaterPath; set { _updaterPath = value; OnPropertyChanged(); } }
    private string _updaterPath = "";

    /// <summary>
    /// 是否启用备份
    /// </summary>
    public bool EnableBackup { get => _enableBackup; set { _enableBackup = value; OnPropertyChanged(); } }
    private bool _enableBackup = true;

    /// <summary>
    /// 备份保留天数
    /// </summary>
    public int BackupRetentionDays { get => _backupRetentionDays; set { _backupRetentionDays = value; OnPropertyChanged(); } }
    private int _backupRetentionDays = 7;

    /// <summary>
    /// 更新临时目录
    /// </summary>
    public string TempUpdateDirectory { get => _tempUpdateDirectory; set { _tempUpdateDirectory = value; OnPropertyChanged(); } }
    private string _tempUpdateDirectory = Path.Combine(Path.GetTempPath(), "ColorVisionUpdate");
}
