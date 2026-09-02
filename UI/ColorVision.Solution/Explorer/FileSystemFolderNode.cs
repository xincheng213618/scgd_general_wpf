using System.IO;

namespace ColorVision.Solution.Explorer
{
    /// <summary>A physical directory view without project membership or solution projection.</summary>
    internal sealed class FileSystemFolderNode : FolderNode
    {
        public bool IsWorkspaceRoot { get; }

        public FileSystemFolderNode(DirectoryInfo directoryInfo, bool isWorkspaceRoot = false)
            : base(directoryInfo)
        {
            IsWorkspaceRoot = isWorkspaceRoot;
            if (isWorkspaceRoot)
            {
                CanReName = false;
                CanDelete = false;
                CanCut = false;
            }
        }

        internal override string? PhysicalDeletePath => IsWorkspaceRoot ? null : base.PhysicalDeletePath;

        public override bool ReName(string name) => !IsWorkspaceRoot && base.ReName(name);

        internal override bool TryDelete(bool showConfirmation) => !IsWorkspaceRoot && base.TryDelete(showConfirmation);
    }
}
