using ColorVision.Common.Utilities;
using ColorVision.Solution.Explorer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace ColorVision.Solution.Workspace
{
    internal enum PrivateWorkspaceKind
    {
        Folder,
        Project,
    }

    /// <summary>
    /// Owns the identity boundary for generated workspaces. A private .cvsln is
    /// an implementation detail and must always resolve back to the folder,
    /// project, or external solution selected by the user.
    /// </summary>
    internal static class PrivateWorkspaceService
    {
        internal const string SourceKindExtensionKey = "WorkspaceSourceKind";
        internal const string SourcePathExtensionKey = "WorkspaceSourcePath";

        public static string CreateWorkspacePath(PrivateWorkspaceKind kind, string sourcePath)
        {
            string workspaceDirectory = GetWorkspaceDirectory(kind);
            Directory.CreateDirectory(workspaceDirectory);
            return GetExpectedWorkspacePath(kind, sourcePath);
        }

        public static bool SetSource(
            SolutionConfig config,
            PrivateWorkspaceKind kind,
            string sourcePath)
        {
            ArgumentNullException.ThrowIfNull(config);
            string normalizedSourcePath = NormalizeSourcePath(kind, sourcePath);
            config.ExtensionData ??= new Dictionary<string, JToken>();

            bool changed = !TryGetString(config.ExtensionData, SourceKindExtensionKey, out string currentKind)
                || !string.Equals(currentKind, kind.ToString(), StringComparison.Ordinal)
                || !TryGetString(config.ExtensionData, SourcePathExtensionKey, out string currentPath)
                || !string.Equals(currentPath, normalizedSourcePath, StringComparison.OrdinalIgnoreCase);
            config.ExtensionData[SourceKindExtensionKey] = new JValue(kind.ToString());
            config.ExtensionData[SourcePathExtensionKey] = new JValue(normalizedSourcePath);
            return changed;
        }

        public static bool TryResolveSourcePath(string? workspacePath, out string sourcePath)
        {
            sourcePath = string.Empty;
            if (string.IsNullOrWhiteSpace(workspacePath)
                || !workspacePath.EndsWith(".cvsln", StringComparison.OrdinalIgnoreCase)
                || !File.Exists(workspacePath))
            {
                return false;
            }

            try
            {
                string fullWorkspacePath = Path.GetFullPath(workspacePath);
                if (!TryGetWorkspaceKind(fullWorkspacePath, out PrivateWorkspaceKind expectedKind))
                    return false;

                SolutionConfig config = SolutionConfigStore.DeserializeAndMigrate(
                    File.ReadAllText(fullWorkspacePath),
                    out _);
                if (config.ExtensionData == null
                    || !TryGetString(config.ExtensionData, SourceKindExtensionKey, out string kindValue)
                    || !Enum.TryParse(kindValue, ignoreCase: false, out PrivateWorkspaceKind actualKind)
                    || actualKind != expectedKind
                    || !TryGetString(config.ExtensionData, SourcePathExtensionKey, out string configuredSourcePath))
                {
                    return false;
                }

                string normalizedSourcePath = NormalizeSourcePath(actualKind, configuredSourcePath);
                bool sourceExists = actualKind == PrivateWorkspaceKind.Folder
                    ? Directory.Exists(normalizedSourcePath)
                    : File.Exists(normalizedSourcePath);
                if (!sourceExists
                    || !string.Equals(
                        fullWorkspacePath,
                        GetExpectedWorkspacePath(actualKind, normalizedSourcePath),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                sourcePath = normalizedSourcePath;
                return true;
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or JsonException
                or InvalidDataException
                or ArgumentException
                or NotSupportedException)
            {
                return false;
            }
        }

        private static bool TryGetWorkspaceKind(
            string workspacePath,
            out PrivateWorkspaceKind kind)
        {
            string? directoryPath = Path.GetDirectoryName(workspacePath);
            foreach (PrivateWorkspaceKind candidate in Enum.GetValues<PrivateWorkspaceKind>())
            {
                if (string.Equals(
                    directoryPath,
                    GetWorkspaceDirectory(candidate),
                    StringComparison.OrdinalIgnoreCase))
                {
                    kind = candidate;
                    return true;
                }
            }

            kind = default;
            return false;
        }

        private static string GetExpectedWorkspacePath(
            PrivateWorkspaceKind kind,
            string sourcePath)
        {
            string normalizedSourcePath = NormalizeSourcePath(kind, sourcePath);
            string sourceKey = Tool.GetMD5(normalizedSourcePath.ToUpperInvariant());
            return Path.Combine(GetWorkspaceDirectory(kind), $"{sourceKey}.cvsln");
        }

        private static string GetWorkspaceDirectory(PrivateWorkspaceKind kind)
        {
            string directoryName = kind switch
            {
                PrivateWorkspaceKind.Folder => "FolderWorkspaces",
                PrivateWorkspaceKind.Project => "ImplicitSolutions",
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ColorVision",
                directoryName);
        }

        private static string NormalizeSourcePath(
            PrivateWorkspaceKind kind,
            string sourcePath)
        {
            string fullPath = Path.GetFullPath(sourcePath);
            return kind == PrivateWorkspaceKind.Folder
                ? Path.TrimEndingDirectorySeparator(fullPath)
                : fullPath;
        }

        private static bool TryGetString(
            IDictionary<string, JToken> extensionData,
            string key,
            out string value)
        {
            value = extensionData.TryGetValue(key, out JToken? token)
                ? token.Value<string>()?.Trim() ?? string.Empty
                : string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }
    }
}
