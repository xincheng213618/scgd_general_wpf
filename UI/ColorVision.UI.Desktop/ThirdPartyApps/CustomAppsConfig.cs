using ColorVision.Common.MVVM;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ColorVision.UI.Desktop.ThirdPartyApps
{
    /// <summary>
    /// 自定义应用/脚本条目（可序列化存储）
    /// </summary>
    public class CustomAppEntry : ViewModelBase
    {
        [Display(Name = "CfgApp_Name", ResourceType = typeof(Properties.Resources))]
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name = string.Empty;

        [Display(Name = "CfgApp_Group", ResourceType = typeof(Properties.Resources))]
        public string Group { get => _Group; set { _Group = value; OnPropertyChanged(); } }
        private string _Group = string.Empty;

        [Display(Name = "CfgApp_Type", ResourceType = typeof(Properties.Resources))]
        public CustomAppType AppType { get => _AppType; set { _AppType = value; OnPropertyChanged(); } }
        private CustomAppType _AppType = CustomAppType.Executable;

        [Display(Name = "CfgApp_PathOrCmd", Description = "CfgApp_PathOrCmdDesc", ResourceType = typeof(Properties.Resources))]
        public string Command { get => _Command; set { _Command = value; OnPropertyChanged(); } }
        private string _Command = string.Empty;

        [Display(Name = "CfgApp_Arguments", ResourceType = typeof(Properties.Resources))]
        public string Arguments { get => _Arguments; set { _Arguments = value; OnPropertyChanged(); } }
        private string _Arguments = string.Empty;

        [Display(Name = "CfgApp_WorkDir", ResourceType = typeof(Properties.Resources))]
        public string WorkingDirectory { get => _WorkingDirectory; set { _WorkingDirectory = value; OnPropertyChanged(); } }
        private string _WorkingDirectory = string.Empty;

        [Display(Name = "CfgApp_Sort", ResourceType = typeof(Properties.Resources))]
        public int Order { get => _Order; set { _Order = value; OnPropertyChanged(); } }
        private int _Order = 100;
    }

    public enum CustomAppType
    {
        [Display(Name = "CfgApp_ExeDesc", ResourceType = typeof(Properties.Resources))]
        Executable,
        [Display(Name = "CfgApp_CmdDesc", ResourceType = typeof(Properties.Resources))]
        CmdScript,
        [Display(Name = "CfgApp_PsDesc", ResourceType = typeof(Properties.Resources))]
        PowerShellScript,
    }

    /// <summary>
    /// 自定义应用配置，自动持久化到 JSON 配置文件
    /// </summary>
    [Display(Name = "CfgApp_ConfigTitle", ResourceType = typeof(Properties.Resources))]
    public class CustomAppsConfig : ViewModelBase, IConfig
    {
        public static CustomAppsConfig Instance => ConfigHandler.GetInstance().GetRequiredService<CustomAppsConfig>();

        [Display(Name = "CfgApp_ListTitle", ResourceType = typeof(Properties.Resources))]
        public ObservableCollection<CustomAppEntry> Entries { get; set; } = new ObservableCollection<CustomAppEntry>();

        [Display(Name = "CfgApp_DefaultCustomGroup", ResourceType = typeof(Properties.Resources))]
        public string DefaultCustomGroup { get => _DefaultCustomGroup; set { _DefaultCustomGroup = value; OnPropertyChanged(); } }
        private string _DefaultCustomGroup = "自定义";

        [Display(Name = "CfgApp_DefaultScriptGroup", ResourceType = typeof(Properties.Resources))]
        public string DefaultScriptGroup { get => _DefaultScriptGroup; set { _DefaultScriptGroup = value; OnPropertyChanged(); } }
        private string _DefaultScriptGroup = "快捷脚本";
    }
}
