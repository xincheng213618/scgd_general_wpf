using System;

namespace ColorVisionSetup
{
    public enum DownloadStage { Checking, Available, Downloading, Verifying, Ready, Launching, Opened, Error, Cancelled }

    public sealed class SetupViewModel : ViewModelBase
    {
        private DownloadStage _stage = DownloadStage.Checking;
        public DownloadStage Stage
        {
            get => _stage;
            set
            {
                SetProperty(ref _stage, value);
                NotifyPropertyChanged(nameof(IsBusy));
                NotifyPropertyChanged(nameof(CanAct));
                NotifyPropertyChanged(nameof(CanRefresh));
                NotifyPropertyChanged(nameof(CanCancel));
                NotifyPropertyChanged(nameof(CanOpenFolder));
                NotifyPropertyChanged(nameof(PrimaryText));
                NotifyPropertyChanged(nameof(ProgressText));
            }
        }
        public bool IsBusy => Stage == DownloadStage.Checking || Stage == DownloadStage.Downloading || Stage == DownloadStage.Verifying || Stage == DownloadStage.Launching;
        public bool CanAct => !IsBusy && Stage != DownloadStage.Opened;
        public bool CanRefresh => !IsBusy;
        public bool CanCancel => IsBusy && Stage != DownloadStage.Launching && !CancelRequested;
        public bool CanOpenFolder => Stage == DownloadStage.Ready || Stage == DownloadStage.Opened;
        private bool _cancelRequested;
        public bool CancelRequested
        {
            get => _cancelRequested;
            set { SetProperty(ref _cancelRequested, value); NotifyPropertyChanged(nameof(CanCancel)); }
        }
        private Version _latestVersion;
        public Version LatestVersion
        {
            get => _latestVersion;
            set { SetProperty(ref _latestVersion, value); NotifyPropertyChanged(nameof(VersionText)); }
        }
        public string VersionText => LatestVersion == null ? "LATEST RELEASE" : "v" + LatestVersion;
        public string PrimaryText
        {
            get
            {
                switch (Stage)
                {
                    case DownloadStage.Checking: return "正在检查版本";
                    case DownloadStage.Downloading: return "正在下载";
                    case DownloadStage.Verifying: return "正在校验";
                    case DownloadStage.Ready: return "打开安装包  →";
                    case DownloadStage.Launching: return "正在打开安装包";
                    case DownloadStage.Opened: return "安装包已打开";
                    case DownloadStage.Error: return "重试  ↗";
                    case DownloadStage.Cancelled: return "重新开始  ↗";
                    default: return "下载最新版  ↓";
                }
            }
        }
        private string _status = "正在连接版本服务器";
        public string Status { get => _status; set => SetProperty(ref _status, value); }
        private string _detail = "即将为你获取 ColorVision 最新版本。";
        public string Detail { get => _detail; set => SetProperty(ref _detail, value); }
        private string _transferText = "检查最新版本";
        public string TransferText { get => _transferText; set => SetProperty(ref _transferText, value); }
        private double _progress;
        public double Progress
        {
            get => _progress;
            set { SetProperty(ref _progress, value); NotifyPropertyChanged(nameof(ProgressText)); }
        }
        private bool _isIndeterminate = true;
        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            set { SetProperty(ref _isIndeterminate, value); NotifyPropertyChanged(nameof(ProgressText)); }
        }
        public string ProgressText => Stage == DownloadStage.Downloading ? (IsIndeterminate ? "下载中" : Progress.ToString("F0") + "%") :
            Stage == DownloadStage.Verifying ? "校验中" : Stage == DownloadStage.Ready || Stage == DownloadStage.Opened ? "100%" : "";
    }
}
