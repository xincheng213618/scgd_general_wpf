using System.Windows.Input;

namespace ColorVision.Solution.Explorer
{
    /// <summary>
    /// Routed project commands shared by the tree, context menus, and future
    /// toolbar/menu surfaces. Providers decide whether a selected project can
    /// execute the corresponding capability.
    /// </summary>
    public static class SolutionProjectCommands
    {
        public const string ActiveConfigurationId = "ActiveSolutionConfiguration";
        public const string BuildSolutionId = "BuildSolution";
        public const string ConfigurationManagerId = "SolutionConfigurationManager";
        public const string DebugStartupProjectId = "DebugStartupProject";
        public const string EditProjectFileId = "EditProjectFile";
        public const string ExcludeFromProjectId = "ExcludeFromProject";
        public const string IncludeInProjectId = "IncludeInProject";
        public const string RunStartupProjectId = "RunStartupProject";
        public const string SetStartupProjectId = "SetStartupProject";
        public const string ShowAllFilesId = "ShowAllProjectFiles";

        public static RoutedUICommand Build { get; } = new("生成项目", nameof(Build), typeof(SolutionProjectCommands));
        public static RoutedUICommand BuildSolution { get; } = new(
            "生成解决方案",
            nameof(BuildSolution),
            typeof(SolutionProjectCommands),
            new InputGestureCollection { new KeyGesture(Key.B, ModifierKeys.Control | ModifierKeys.Shift) });
        public static RoutedUICommand Run { get; } = new(
            "运行项目",
            nameof(Run),
            typeof(SolutionProjectCommands),
            new InputGestureCollection { new KeyGesture(Key.F5, ModifierKeys.Control) });
        public static RoutedUICommand Debug { get; } = new(
            "调试项目",
            nameof(Debug),
            typeof(SolutionProjectCommands),
            new InputGestureCollection { new KeyGesture(Key.F5) });
        public static RoutedUICommand ConfigurationManager { get; } = new(
            "配置管理器",
            nameof(ConfigurationManager),
            typeof(SolutionProjectCommands));
        public static RoutedUICommand SetStartupProject { get; } = new("设为启动项目", nameof(SetStartupProject), typeof(SolutionProjectCommands));
        public static RoutedUICommand ExcludeFromProject { get; } = new("从项目中排除", nameof(ExcludeFromProject), typeof(SolutionProjectCommands));
        public static RoutedUICommand IncludeInProject { get; } = new("包括在项目中", nameof(IncludeInProject), typeof(SolutionProjectCommands));

        public static ICommand? GetCommand(string capabilityId)
        {
            return capabilityId.ToLowerInvariant() switch
            {
                ProjectCapabilityIds.Build => Build,
                ProjectCapabilityIds.Run => Run,
                ProjectCapabilityIds.Debug => Debug,
                _ => null,
            };
        }

        public static string? GetCapabilityId(ICommand command)
        {
            if (command == Build) return ProjectCapabilityIds.Build;
            if (command == Run) return ProjectCapabilityIds.Run;
            if (command == Debug) return ProjectCapabilityIds.Debug;
            return null;
        }
    }
}
