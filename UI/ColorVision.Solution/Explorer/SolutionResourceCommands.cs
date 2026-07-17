using ColorVision.Solution.Editor;
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
        public const string ImportedSourceMenuId = "ImportedSolutionSource";
        public const string EditImportedSourceId = "EditImportedSolutionSource";
        public const string RevealImportedSourceId = "RevealImportedSolutionSource";
        public const string CopyImportedSourcePathId = "CopyImportedSolutionSourcePath";

        public static RoutedUICommand Open { get; } = new(
            "打开",
            nameof(Open),
            typeof(SolutionResourceCommands),
            new InputGestureCollection { new KeyGesture(Key.Enter) });

        public static RoutedUICommand OpenWith { get; } = new(
            "打开方式",
            nameof(OpenWith),
            typeof(SolutionResourceCommands));
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
