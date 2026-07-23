using ColorVision.Solution;
using ColorVision.Solution.Editor;
using ColorVision.Solution.Editor.AvalonEditor;
using ColorVision.Solution.Workspace;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace ColorVision.Copilot
{
    internal sealed record CopilotLocalFileLinkTarget(string FilePath, int? LineNumber, int? ColumnNumber);

    internal static class CopilotLocalFileLinkNavigator
    {
        private const int MaximumTargetCharacters = 4096;
        private static readonly Regex TargetRegex = new(@"^(?<path>.+?)(?:(?:#L|:)(?<line>\d+)(?:(?:C|:)(?<column>\d+))?)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool TryResolve(string? value, out CopilotLocalFileLinkTarget target)
        {
            target = null!;
            var candidate = (value ?? string.Empty).Trim();
            if (candidate.Length == 0 || candidate.Length > MaximumTargetCharacters)
                return false;

            var match = TargetRegex.Match(candidate);
            if (!match.Success || !TryGetWorkspaceRoot(out var workspaceRoot))
                return false;

            try
            {
                var pathValue = match.Groups["path"].Value.Trim();
                string fullPath;
                if (Uri.TryCreate(pathValue, UriKind.Absolute, out var fileUri) && fileUri.IsFile)
                {
                    if (fileUri.IsUnc)
                        return false;
                    fullPath = Path.GetFullPath(fileUri.LocalPath);
                }
                else
                {
                    pathValue = Uri.UnescapeDataString(pathValue);
                    fullPath = Path.IsPathRooted(pathValue)
                        ? Path.GetFullPath(pathValue)
                        : Path.GetFullPath(pathValue, workspaceRoot);
                }

                if (!IsCurrentWorkspaceFile(fullPath, workspaceRoot))
                    return false;

                target = new CopilotLocalFileLinkTarget(
                    fullPath,
                    TryParsePositiveNumber(match.Groups["line"].Value),
                    TryParsePositiveNumber(match.Groups["column"].Value));
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or PathTooLongException or UriFormatException)
            {
                return false;
            }
        }

        public static string BuildToolTip(CopilotLocalFileLinkTarget target)
        {
            if (target.LineNumber == null)
                return target.FilePath;

            return target.ColumnNumber == null
                ? $"{target.FilePath}:{target.LineNumber}"
                : $"{target.FilePath}:{target.LineNumber}:{target.ColumnNumber}";
        }

        public static bool TryOpen(CopilotLocalFileLinkTarget target, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                if (!TryGetWorkspaceRoot(out var workspaceRoot) || !IsCurrentWorkspaceFile(target.FilePath, workspaceRoot))
                    throw new InvalidOperationException("文件已不存在或不在当前工作区内。");

                if (!ResourceOpenService.Instance.TryOpen(target.FilePath))
                    throw new InvalidOperationException("当前没有可用于打开此文件的编辑器。");

                if (target.LineNumber is > 0
                    && WorkspaceManager.LayoutDocumentPane != null
                    && WorkspaceManager.FindDocumentActive(WorkspaceManager.LayoutDocumentPane)?.Content is AvalonEditControll textEditor)
                {
                    textEditor.NavigateTo(target.LineNumber.Value, target.ColumnNumber ?? 1);
                }
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        private static bool TryGetWorkspaceRoot(out string workspaceRoot)
        {
            workspaceRoot = SolutionManager.GetInstance().CurrentSolutionExplorer?.DirectoryInfo?.FullName ?? string.Empty;
            return !string.IsNullOrWhiteSpace(workspaceRoot);
        }

        private static bool IsCurrentWorkspaceFile(string filePath, string workspaceRoot) =>
            File.Exists(filePath) && CopilotWorkspaceSearchSupport.IsPathWithinRoots(filePath, new[] { workspaceRoot });

        private static int? TryParsePositiveNumber(string value) =>
            int.TryParse(value, out var number) && number > 0 ? number : null;
    }
}
