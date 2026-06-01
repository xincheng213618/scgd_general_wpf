using System;
using System.Collections.Generic;

namespace ColorVision.Update
{
    public class UpdateBackupManifest
    {
        public DateTime CreatedAt { get; set; }
        public string ProgramDirectory { get; set; } = string.Empty;
        public string VersionBefore { get; set; } = string.Empty;
        public string VersionTarget { get; set; } = string.Empty;
        public List<UpdateBackupFileEntry> Files { get; set; } = new();
        public List<UpdateBackupDirectoryEntry> Directories { get; set; } = new();
    }

    public class UpdateBackupFileEntry
    {
        public string RelativePath { get; set; } = string.Empty;
        public string OriginalPath { get; set; } = string.Empty;
        public string BackupPath { get; set; } = string.Empty;
        public bool ExistsBeforeUpdate { get; set; }
        public long Size { get; set; }
        public string Sha256 { get; set; } = string.Empty;
    }

    public class UpdateBackupDirectoryEntry
    {
        public string RelativePath { get; set; } = string.Empty;
        public string OriginalPath { get; set; } = string.Empty;
        public string BackupPath { get; set; } = string.Empty;
        public bool ExistsBeforeUpdate { get; set; }
    }
}