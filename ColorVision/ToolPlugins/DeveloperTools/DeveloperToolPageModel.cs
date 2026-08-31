using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.DeveloperTools;
using System.Collections.ObjectModel;

namespace ColorVision.ToolPlugins.DeveloperTools
{
    public sealed class DeveloperToolPageModel : ViewModelBase
    {
        public DeveloperToolPageModel(DeveloperToolKind kind) { Kind = kind; }
        public DeveloperToolKind Kind { get; }
        public string Title => Kind == DeveloperToolKind.Python ? "Python" : "Node.js / npm";
        public string Description => Kind == DeveloperToolKind.Python
            ? "查看系统里的 Python 与 pip，按需安装官方 Windows 版本。"
            : "查看系统里的 Node.js 与 npm；安装 Node.js 时一并提供 npm。";
        public string InstallHint => Kind == DeveloperToolKind.Python
            ? "使用官方安装向导的默认路径；如需在终端直接输入 python，请在向导中选择添加到 PATH。已有版本不会由本窗口自动删除。"
            : "使用官方 MSI 安装向导的默认路径，包含 npm。安装可能升级或替换现有 Node.js；使用 nvm / fnm / Volta 时请优先由原管理器维护。";
        public string RegistryHint => Kind == DeveloperToolKind.Python
            ? "这里选择的是 Python 安装包下载源，不会更改 pip 的软件包源。"
            : "这里选择的是 Node.js 安装包下载源，不会更改 npm registry，也不会安装 cnpm。";
        public ObservableCollection<DeveloperToolInstallation> Installations { get; } = new();
        public ObservableCollection<DeveloperToolRelease> Releases { get; } = new();
        public DeveloperToolRelease? SelectedRelease
        {
            get => _selectedRelease;
            set { SetProperty(ref _selectedRelease, value); OnPropertyChanged(nameof(CanInstall)); }
        }
        private DeveloperToolRelease? _selectedRelease;
        public bool CanInstall => SelectedRelease != null;
        public int SelectedSourceIndex { get; set; }
        public string DetectionStatus { get => _detectionStatus; private set => SetProperty(ref _detectionStatus, value); }
        private string _detectionStatus = "等待检测";
        public string CurrentCommandPath { get => _currentCommandPath; private set => SetProperty(ref _currentCommandPath, value); }
        private string _currentCommandPath = "—";
        public string RefreshedCommandPath { get => _refreshedCommandPath; private set => SetProperty(ref _refreshedCommandPath, value); }
        private string _refreshedCommandPath = "—";
        public string Note { get => _note; private set => SetProperty(ref _note, value); }
        private string _note = "";
        public string CatalogStatus { get => _catalogStatus; set => SetProperty(ref _catalogStatus, value); }
        private string _catalogStatus = "点击“获取可安装版本”从官网读取稳定版本；检测系统环境不需要联网。";

        public void Apply(DeveloperToolSnapshot snapshot)
        {
            Installations.Clear();
            foreach (var installation in snapshot.Installations) Installations.Add(installation);
            DetectionStatus = Installations.Count == 0 ? "未检测到解释器" : $"检测到 {Installations.Count} 处安装";
            CurrentCommandPath = string.IsNullOrEmpty(snapshot.CurrentCommandPath) ? "未在应用当前 PATH 中找到" : snapshot.CurrentCommandPath;
            RefreshedCommandPath = string.IsNullOrEmpty(snapshot.RefreshedCommandPath) ? "未在系统登记 PATH 中找到" : snapshot.RefreshedCommandPath;
            Note = snapshot.Note + (string.IsNullOrEmpty(snapshot.PackageManagerPath) ? "" : $"\nnpm 命令路径：{snapshot.PackageManagerPath}");
        }
    }
}
