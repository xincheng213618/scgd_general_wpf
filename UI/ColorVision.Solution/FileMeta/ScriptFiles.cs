using ColorVision.Common.NativeMethods;
using System.IO;

namespace ColorVision.Solution.FileMeta
{
    /// <summary>
    /// File meta for Python script files (.py, .pyw).
    /// Adds "Run in Terminal" context menu item.
    /// </summary>
    [FileMetaForExtension(".py|.pyw", name: "Python Script", isDefault: true)]
    public class PythonFile : FileMetaBase, IScriptFileMeta
    {
        public PythonFile() { }

        public PythonFile(FileInfo fileInfo)
        {
            FileInfo = fileInfo;
            Name = fileInfo.Name;
            Icon = FileIcon.GetFileIconImageSource(fileInfo.FullName);
        }
    }

    /// <summary>
    /// File meta for PowerShell script files (.ps1).
    /// </summary>
    [FileMetaForExtension(".ps1", name: "PowerShell Script", isDefault: true)]
    public class PowerShellFile : FileMetaBase, IScriptFileMeta
    {
        public PowerShellFile() { }

        public PowerShellFile(FileInfo fileInfo)
        {
            FileInfo = fileInfo;
            Name = fileInfo.Name;
            Icon = FileIcon.GetFileIconImageSource(fileInfo.FullName);
        }
    }

    /// <summary>
    /// File meta for batch/cmd script files (.bat, .cmd).
    /// </summary>
    [FileMetaForExtension(".bat|.cmd", name: "Batch Script", isDefault: true)]
    public class BatchFile : FileMetaBase, IScriptFileMeta
    {
        public BatchFile() { }

        public BatchFile(FileInfo fileInfo)
        {
            FileInfo = fileInfo;
            Name = fileInfo.Name;
            Icon = FileIcon.GetFileIconImageSource(fileInfo.FullName);
        }
    }
}
