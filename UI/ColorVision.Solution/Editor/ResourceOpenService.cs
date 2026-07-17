using System.IO;

namespace ColorVision.Solution.Editor
{
    public enum ResourceOpenKind
    {
        Missing,
        File,
        Folder,
        Solution,
        Project,
    }

    /// <summary>
    /// Canonical application-level open router. It separates workspace/project
    /// activation from editor selection so callers do not need extension logic.
    /// </summary>
    public sealed class ResourceOpenService
    {
        public static ResourceOpenService Instance { get; } = new();
        private readonly EditorManager _editorManager = EditorManager.Instance;

        private ResourceOpenService()
        {
        }

        public bool TryOpen(string path)
        {
            return Classify(path) switch
            {
                ResourceOpenKind.Folder => SolutionManager.GetInstance().OpenFolder(path),
                ResourceOpenKind.Solution => SolutionManager.GetInstance().OpenSolution(path),
                ResourceOpenKind.Project => SolutionManager.GetInstance().OpenProject(path),
                ResourceOpenKind.File => _editorManager.TryOpenFile(path),
                _ => false,
            };
        }

        /// <summary>
        /// Opens an existing physical resource with one explicit editor. This
        /// intentionally bypasses solution/project activation: choosing an
        /// editor is an explicit user decision, not extension-based routing.
        /// </summary>
        public bool TryOpenWith(string path, string editorId)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(editorId))
                return false;
            if (Directory.Exists(path))
                return _editorManager.OpenFolderWith(path, editorId);
            return File.Exists(path) && _editorManager.OpenFileWith(path, editorId);
        }

        public static bool TryOpenFile(string filePath) => EditorManager.Instance.TryOpenFile(filePath);

        internal static ResourceOpenKind Classify(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return ResourceOpenKind.Missing;
            if (Directory.Exists(path))
                return ResourceOpenKind.Folder;
            if (!File.Exists(path))
                return ResourceOpenKind.Missing;
            if (SolutionManager.IsSolutionFilePath(path))
                return ResourceOpenKind.Solution;
            if (SolutionManager.IsProjectFilePath(path))
                return ResourceOpenKind.Project;
            return ResourceOpenKind.File;
        }
    }
}
