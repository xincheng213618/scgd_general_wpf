using System.Windows.Input;

namespace ColorVision.Solution.Explorer
{
    [Flags]
    public enum SolutionContainerAction
    {
        None = 0,
        AddNewItem = 1 << 0,
        AddExistingItem = 1 << 1,
        CreateFolder = 1 << 2,
        AddNewProject = 1 << 3,
        AddExistingProject = 1 << 4,
        CreateSolutionFolder = 1 << 5,
    }

    /// <summary>
    /// Capability contract for nodes that can contain solution resources.
    /// Logical and physical containers expose the actions they actually support
    /// instead of relying on node types or FullPath conventions.
    /// </summary>
    public interface ISolutionContainerNode
    {
        SolutionContainerAction SupportedContainerActions { get; }
        void ExecuteContainerAction(SolutionContainerAction action);
    }

    /// <summary>
    /// A node with an explicit physical directory that accepts clipboard paste.
    /// Logical solution folders intentionally do not implement this contract.
    /// </summary>
    public interface ISolutionPhysicalContainer
    {
        string PhysicalContainerPath { get; }
    }

    public static class SolutionContainerCommands
    {
        public const string AddMenuId = "Add";
        public const string AddNewItemId = "AddNewItem";
        public const string AddExistingItemId = "AddExistingItem";
        public const string CreateFolderId = "AddFolder";
        public const string AddNewProjectId = "AddNewProject";
        public const string AddExistingProjectId = "AddExistingProject";
        public const string CreateSolutionFolderId = "AddSolutionFolder";

        public static RoutedUICommand AddNewItem { get; } = CreateCommand("新建项", nameof(AddNewItem));
        public static RoutedUICommand AddExistingItem { get; } = CreateCommand("添加现有项", nameof(AddExistingItem));
        public static RoutedUICommand CreateFolder { get; } = CreateCommand("新建文件夹", nameof(CreateFolder));
        public static RoutedUICommand AddNewProject { get; } = CreateCommand("新建项目", nameof(AddNewProject));
        public static RoutedUICommand AddExistingProject { get; } = CreateCommand("添加现有项目", nameof(AddExistingProject));
        public static RoutedUICommand CreateSolutionFolder { get; } = CreateCommand("新建解决方案文件夹", nameof(CreateSolutionFolder));

        internal static bool TryGetAction(ICommand command, out SolutionContainerAction action)
        {
            action = command switch
            {
                var value when value == AddNewItem => SolutionContainerAction.AddNewItem,
                var value when value == AddExistingItem => SolutionContainerAction.AddExistingItem,
                var value when value == CreateFolder => SolutionContainerAction.CreateFolder,
                var value when value == AddNewProject => SolutionContainerAction.AddNewProject,
                var value when value == AddExistingProject => SolutionContainerAction.AddExistingProject,
                var value when value == CreateSolutionFolder => SolutionContainerAction.CreateSolutionFolder,
                _ => SolutionContainerAction.None,
            };
            return action != SolutionContainerAction.None;
        }

        internal static bool Supports(this ISolutionContainerNode container, SolutionContainerAction action)
        {
            return action != SolutionContainerAction.None
                && (container.SupportedContainerActions & action) == action;
        }

        private static RoutedUICommand CreateCommand(string text, string name)
        {
            return new RoutedUICommand(text, name, typeof(SolutionContainerCommands));
        }
    }
}
