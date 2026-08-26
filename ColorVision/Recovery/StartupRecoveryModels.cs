using ColorVision.UI.Plugins;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ColorVision.Recovery
{
    public enum StartupRecoveryAction
    {
        NormalStart,
        SkipSelectedOnce,
        SkipAllOnce,
        DisableSelectedAndStart,
        RunSetupWizard,
        Exit,
    }

    public sealed record StartupRecoveryPluginSelection(
        string PluginKey,
        string? PluginId,
        string DirectoryName,
        string DirectoryPath,
        bool IsLegacy);

    public sealed record StartupRecoveryResult(
        StartupRecoveryAction Action,
        IReadOnlyList<StartupRecoveryPluginSelection> SelectedPlugins)
    {
        public IReadOnlyList<string> SelectedPluginKeys =>
            SelectedPlugins.Select(item => item.PluginKey).ToArray();

        public static StartupRecoveryResult Exit { get; } =
            new(StartupRecoveryAction.Exit, Array.Empty<StartupRecoveryPluginSelection>());
    }

    public sealed class StartupRecoveryPluginItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isRecoveryBusy;
        private string _backupText = "无备份";
        private PluginRecoveryBackupInfo? _backup;

        public event PropertyChangedEventHandler? PropertyChanged;

        public required string PluginKey { get; init; }

        public required string? PluginId { get; init; }

        public required string DirectoryName { get; init; }

        public required string DirectoryPath { get; init; }

        public required string DisplayName { get; init; }

        public required string VersionText { get; init; }

        public required bool IsEnabled { get; init; }

        public required DateTime LastWriteTime { get; init; }

        public required bool IsLegacy { get; init; }

        public required bool HasInvalidManifest { get; init; }

        public bool IsBackupOnly { get; init; }

        public required bool IsSuspected { get; set; }

        public string IdText => PluginId ?? DirectoryName;

        public string EnabledText => IsBackupOnly ? "目录缺失" : IsEnabled ? "启用" : "已禁用";

        public string LastWriteTimeText => LastWriteTime == DateTime.MinValue
            ? "未知"
            : LastWriteTime.ToString("yyyy-MM-dd HH:mm");

        public string SourceText => IsBackupOnly
            ? "仅备份可恢复"
            : HasInvalidManifest
            ? "清单异常"
            : IsLegacy ? "旧式目录" : "插件清单";

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (IsBackupOnly && value)
                    return;

                if (_isSelected == value)
                    return;

                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public bool CanRestoreBackup => _backup != null && !_isRecoveryBusy;

        public string BackupText
        {
            get => _backupText;
            private set
            {
                if (_backupText == value)
                    return;

                _backupText = value;
                OnPropertyChanged();
            }
        }

        public PluginRecoveryBackupInfo? Backup => _backup;

        public StartupRecoveryPluginSelection ToSelection() => new(
            PluginKey,
            PluginId,
            DirectoryName,
            DirectoryPath,
            IsLegacy);

        public void SetBackup(PluginRecoveryBackupInfo? backup)
        {
            _backup = backup;
            BackupText = backup == null ? "无备份" : "验证并回退";
            OnPropertyChanged(nameof(Backup));
            OnPropertyChanged(nameof(CanRestoreBackup));
        }

        public void SetRecoveryBusy(bool isRecoveryBusy)
        {
            if (_isRecoveryBusy == isRecoveryBusy)
                return;

            _isRecoveryBusy = isRecoveryBusy;
            OnPropertyChanged(nameof(CanRestoreBackup));
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
