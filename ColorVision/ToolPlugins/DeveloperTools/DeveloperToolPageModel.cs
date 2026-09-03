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
        public string InstallHint => Kind == DeveloperToolKind.Python
            ? "使用官方安装向导；需要在终端输入 python 时，请勾选“添加到 PATH”。"
            : "官方 MSI 包含 npm；使用 nvm、fnm 或 Volta 时，请继续由原版本管理器维护。";
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
        private string _catalogStatus = "尚未获取可安装版本。";

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
