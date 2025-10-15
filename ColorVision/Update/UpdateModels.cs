namespace ColorVision.Update;

// 数据模型类 - 与更新器中的定义保持一致
public class UpdateManifest
{
    public string Version { get; set; } = "1.0";
    public UpdateType UpdateType { get; set; }
    public UpdateInfo UpdateInfo { get; set; } = new();
    public PathConfiguration Paths { get; set; } = new();
    public ExecutableConfiguration Executable { get; set; } = new();
    public UpdateOptions Options { get; set; } = new();
    public List<FileOperation> Files { get; set; } = new();
    public DateTime Timestamp { get; set; }
    public string? Signature { get; set; }
}

public enum UpdateType
{
    Application,
    Plugin
}

public class UpdateInfo
{
    public string Version { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}

public class PathConfiguration
{
    public string SourcePath { get; set; } = "";
    public string TargetPath { get; set; } = "";
    public string BackupPath { get; set; } = "";
}

public class ExecutableConfiguration
{
    public string Name { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
}

public class UpdateOptions
{
    public bool CreateBackup { get; set; } = true;
    public bool VerifyFiles { get; set; } = true;
    public bool RestartAfterUpdate { get; set; } = true;
    public bool CleanupOnSuccess { get; set; } = true;
    public bool RollbackOnFailure { get; set; } = true;
}

public class FileOperation
{
    public string Source { get; set; } = "";
    public string Target { get; set; } = "";
    public FileAction Action { get; set; } = FileAction.Replace;
    public bool Verify { get; set; } = true;
    public bool Critical { get; set; } = true;
    public string? ExpectedHash { get; set; }
}

public enum FileAction
{
    Replace,
    Add,
    Delete
}
