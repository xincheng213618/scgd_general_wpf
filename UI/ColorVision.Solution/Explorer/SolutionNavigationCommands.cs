using System.Windows.Input;

namespace ColorVision.Solution.Explorer
{
    public static class SolutionNavigationCommands
    {
        public const string RevealInTreeId = "RevealInSolutionTree";

        public static RoutedUICommand RevealInTree { get; } = new(
            "在解决方案资源管理器中定位",
            nameof(RevealInTree),
            typeof(SolutionNavigationCommands));

        public static RoutedUICommand SyncWithActiveDocument { get; } = new(
            "与活动文档同步",
            nameof(SyncWithActiveDocument),
            typeof(SolutionNavigationCommands));

        public static RoutedUICommand CollapseAll { get; } = new(
            "全部折叠",
            nameof(CollapseAll),
            typeof(SolutionNavigationCommands));

        public static RoutedUICommand Refresh { get; } = new(
            "刷新资源管理器",
            nameof(Refresh),
            typeof(SolutionNavigationCommands));
    }
}
