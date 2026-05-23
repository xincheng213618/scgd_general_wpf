using System;
using System.Collections.Generic;

namespace ColorVision.Update
{
    public enum UpdateApplyState
    {
        None,
        Prepared,
        Applying,
        Applied,
        Completed,
        Failed,
        RolledBack
    }

    public class UpdateStateInfo
    {
        public UpdateApplyState State { get; set; }
        public string BackupPath { get; set; } = string.Empty;
        public string StagePath { get; set; } = string.Empty;
        public string TargetPath { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string VersionBefore { get; set; } = string.Empty;
        public string VersionTarget { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public List<string> PackagePaths { get; set; } = new();
        public List<string> PluginPackagePaths { get; set; } = new();
    }

    public sealed class UpdateBackupPrepareResult
    {
        public required string BackupPath { get; init; }
        public required string ManifestPath { get; init; }
        public required string StateFilePath { get; init; }
        public required string ApplyingStatePath { get; init; }
        public required string AppliedStatePath { get; init; }
        public required string FailedStatePath { get; init; }
        public required UpdateStateInfo State { get; init; }
    }
}