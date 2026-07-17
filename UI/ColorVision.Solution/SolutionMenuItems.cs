using ColorVision.Common.MVVM;
using ColorVision.Solution.Explorer;
using ColorVision.UI.Menus;
using ColorVision.UI.Menus.Base;
using System.Windows.Input;

namespace ColorVision.Solution
{
    public static class SolutionWorkspaceCommands
    {
        public static RoutedUICommand OpenFolder { get; } = new(
            "打开文件夹",
            nameof(OpenFolder),
            typeof(SolutionWorkspaceCommands));

        public static RoutedUICommand CloseSolution { get; } = new(
            "关闭解决方案",
            nameof(CloseSolution),
            typeof(SolutionWorkspaceCommands));
    }

    public sealed class MenuCloseSolution : MenuItemFileBase
    {
        public override string GuidId => nameof(MenuCloseSolution);
        public override string Header => "关闭解决方案(_L)";
        public override int Order => 21;
        public override ICommand Command => SolutionWorkspaceCommands.CloseSolution;
    }

    internal static class SolutionMenuIds
    {
        public const string Build = "Solution.BuildMenu";
        public const string Debug = "Solution.DebugMenu";
        public const string Configuration = "Solution.ConfigurationMenu";
    }

    public sealed class MenuSolutionBuild : GlobalMenuBase
    {
        public override string GuidId => SolutionMenuIds.Build;
        public override string Header => "生成(_B)";
        public override int Order => 4;
    }

    public sealed class MenuBuildSolution : MenuItemBase
    {
        public override string TargetName => MenuItemConstants.GlobalTarget;
        public override string OwnerGuid => SolutionMenuIds.Build;
        public override string Header => "生成解决方案(_B)";
        public override string InputGestureText => "Ctrl+Shift+B";
        public override int Order => 10;
        public override System.Windows.Input.ICommand Command => SolutionProjectCommands.BuildSolution;
    }

    public sealed class MenuSolutionConfiguration : MenuItemBase
    {
        public override string TargetName => MenuItemConstants.GlobalTarget;
        public override string OwnerGuid => SolutionMenuIds.Build;
        public override string GuidId => SolutionMenuIds.Configuration;
        public override string Header => "活动解决方案配置(_C)";
        public override int Order => 20;
    }

    public sealed class MenuSolutionConfigurationManager : MenuItemBase
    {
        public override string TargetName => MenuItemConstants.GlobalTarget;
        public override string OwnerGuid => SolutionMenuIds.Build;
        public override string Header => "配置管理器(_M)...";
        public override int Order => 30;
        public override System.Windows.Input.ICommand Command => SolutionProjectCommands.ConfigurationManager;
    }

    public sealed class SolutionConfigurationMenuProvider : IMenuItemProvider
    {
        public IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            SolutionExplorer? explorer = SolutionManager.GetInstance().CurrentSolutionExplorer;
            if (explorer == null)
                return Array.Empty<MenuItemMetadata>();

            return explorer.GetAvailableSolutionConfigurations()
                .Select((configuration, index) => new MenuItemMetadata
                {
                    TargetName = MenuItemConstants.GlobalTarget,
                    OwnerGuid = SolutionMenuIds.Configuration,
                    GuidId = $"Solution.Configuration.{configuration}",
                    Header = configuration,
                    Order = index,
                    IsChecked = string.Equals(
                        explorer.ActiveConfiguration,
                        configuration,
                        StringComparison.OrdinalIgnoreCase),
                    Command = new RelayCommand(
                        _ => explorer.SetActiveConfiguration(configuration),
                        _ => ReferenceEquals(
                            explorer,
                            SolutionManager.GetInstance().CurrentSolutionExplorer)),
                })
                .ToList();
        }
    }

    public sealed class MenuSolutionDebug : GlobalMenuBase
    {
        public override string GuidId => SolutionMenuIds.Debug;
        public override string Header => "调试(_D)";
        public override int Order => 4;
    }

    public sealed class MenuDebugStartupProject : MenuItemBase
    {
        public override string TargetName => MenuItemConstants.GlobalTarget;
        public override string OwnerGuid => SolutionMenuIds.Debug;
        public override string Header => "开始调试(_S)";
        public override string InputGestureText => "F5";
        public override int Order => 10;
        public override System.Windows.Input.ICommand Command => SolutionProjectCommands.Debug;
    }

    public sealed class MenuRunStartupProject : MenuItemBase
    {
        public override string TargetName => MenuItemConstants.GlobalTarget;
        public override string OwnerGuid => SolutionMenuIds.Debug;
        public override string Header => "开始执行(不调试)(_H)";
        public override string InputGestureText => "Ctrl+F5";
        public override int Order => 20;
        public override System.Windows.Input.ICommand Command => SolutionProjectCommands.Run;
    }
}
