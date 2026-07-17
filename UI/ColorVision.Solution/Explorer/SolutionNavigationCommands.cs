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
    }
}
