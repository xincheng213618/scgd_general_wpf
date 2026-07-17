using ColorVision.Common.Utilities;
using ColorVision.Solution.Editor;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;

namespace ColorVision.Solution.Explorer
{
    /// <summary>
    /// Routed commands owned by the solution tree. They intentionally do not use
    /// ApplicationCommands.Open, whose Ctrl+O gesture opens the global picker.
    /// </summary>
    public static class SolutionResourceCommands
    {
        public const string OpenId = "Open";
        public const string OpenWithId = "OpenWith";
        public const string RunScriptId = "RunScript";
        public const string RevealInFileExplorerId = "RevealInFileExplorer";
        public const string OpenInTerminalId = "OpenInTerminal";
        public static RoutedUICommand Open { get; } = new(
            "打开",
            nameof(Open),
            typeof(SolutionResourceCommands),
            new InputGestureCollection { new KeyGesture(Key.Enter) });

        public static RoutedUICommand OpenWith { get; } = new(
            "打开方式",
            nameof(OpenWith),
            typeof(SolutionResourceCommands));

        public static RoutedUICommand RunScript { get; } = new(
            "运行脚本",
            nameof(RunScript),
            typeof(SolutionResourceCommands));

        public static RoutedUICommand RevealInFileExplorer { get; } = new(
            "在文件资源管理器中打开",
            nameof(RevealInFileExplorer),
            typeof(SolutionResourceCommands));

        public static RoutedUICommand OpenInTerminal { get; } = new(
            "在终端中打开",
            nameof(OpenInTerminal),
            typeof(SolutionResourceCommands));
    }

    internal static class SolutionResourceShellPolicy
    {
        public static bool CanReveal(SolutionNode node)
        {
            ArgumentNullException.ThrowIfNull(node);
            return node.ExplorerResourcePath is { } path
                && (File.Exists(path) || Directory.Exists(path));
        }

        public static bool TryReveal(SolutionNode node)
        {
            if (!CanReveal(node) || node.ExplorerResourcePath is not { } path)
                return false;

            try
            {
                if (File.Exists(path))
                    PlatformHelper.OpenFolderAndSelectFile(path);
                else
                    PlatformHelper.OpenFolder(path);
                return true;
            }
            catch (Exception ex) when (ex is Win32Exception
                or InvalidOperationException
                or ArgumentException
                or NotSupportedException)
            {
                return false;
            }
        }

        public static bool CanOpenTerminal(SolutionNode node)
        {
            ArgumentNullException.ThrowIfNull(node);
            return node.TerminalWorkingDirectory is { } path && Directory.Exists(path);
        }

        public static bool TryOpenTerminal(SolutionNode node)
        {
            if (!CanOpenTerminal(node) || node.TerminalWorkingDirectory is not { } path)
                return false;

            try
            {
                return Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/K cd /d \"{path}\"",
                    UseShellExecute = true,
                }) != null;
            }
            catch (Exception ex) when (ex is Win32Exception
                or InvalidOperationException
                or ArgumentException)
            {
                return false;
            }
        }
    }

    internal static class SolutionResourceOpenPolicy
    {
        public static bool CanOpen(IReadOnlyList<SolutionNode> nodes)
        {
            ArgumentNullException.ThrowIfNull(nodes);
            if (nodes is [var node])
                return node.CanOpen;
            return TryGetBatchResourcePaths(nodes, out _);
        }

        public static bool TryGetBatchResourcePaths(
            IReadOnlyList<SolutionNode> nodes,
            out string[] resourcePaths)
        {
            ArgumentNullException.ThrowIfNull(nodes);
            resourcePaths = nodes
                .Select(node => node.EditorResourcePath)
                .OfType<string>()
                .ToArray();
            return nodes.Count > 1
                && nodes.All(node => node.CanOpen)
                && resourcePaths.Length == nodes.Count
                && ResourceOpenService.CanOpenTogether(resourcePaths);
        }
    }
}
