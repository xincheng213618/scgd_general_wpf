using ColorVision.Common.MVVM;
using ColorVision.UI;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ColorVision.UI.Desktop.ThirdPartyApps
{
    /// <summary>
    /// 自定义应用/脚本条目（可序列化存储）
    /// </summary>
    public class CustomAppEntry : ViewModelBase
    {
        [DisplayName("名称")]
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name = string.Empty;

        [DisplayName("分组")]
        public string Group { get => _Group; set { _Group = value; OnPropertyChanged(); } }
        private string _Group = string.Empty;

        [DisplayName("类型")]
        public CustomAppType AppType { get => _AppType; set { _AppType = value; OnPropertyChanged(); } }
        private CustomAppType _AppType = CustomAppType.Executable;

        [DisplayName("路径/命令")]
        [Description("可执行文件路径或脚本命令")]
        public string Command { get => _Command; set { _Command = value; OnPropertyChanged(); } }
        private string _Command = string.Empty;

        [DisplayName("参数")]
        public string Arguments { get => _Arguments; set { _Arguments = value; OnPropertyChanged(); } }
        private string _Arguments = string.Empty;

        [DisplayName("工作目录")]
        public string WorkingDirectory { get => _WorkingDirectory; set { _WorkingDirectory = value; OnPropertyChanged(); } }
        private string _WorkingDirectory = string.Empty;

        [DisplayName("排序")]
        public int Order { get => _Order; set { _Order = value; OnPropertyChanged(); } }
        private int _Order = 100;
    }

    public enum CustomAppType
    {
        [Description("可执行文件")]
        Executable,
        [Description("CMD 脚本")]
        CmdScript,
        [Description("PowerShell 脚本")]
        PowerShellScript,
    }

    /// <summary>
    /// 自定义应用配置，自动持久化到 JSON 配置文件
    /// </summary>
    [DisplayName("自定义应用配置")]
    public class CustomAppsConfig : ViewModelBase, IConfig
    {
        public static CustomAppsConfig Instance => ConfigHandler.GetInstance().GetRequiredService<CustomAppsConfig>();

        [DisplayName("自定义应用列表")]
        public ObservableCollection<CustomAppEntry> Entries { get; set; } = new ObservableCollection<CustomAppEntry>();

        [DisplayName("默认自定义分组名")]
        public string DefaultCustomGroup { get => _DefaultCustomGroup; set { _DefaultCustomGroup = value; OnPropertyChanged(); } }
        private string _DefaultCustomGroup = "自定义";

        [DisplayName("默认脚本分组名")]
        public string DefaultScriptGroup { get => _DefaultScriptGroup; set { _DefaultScriptGroup = value; OnPropertyChanged(); } }
        private string _DefaultScriptGroup = "快捷脚本";
    }
}
