using ColorVision.Common.MVVM;
using ColorVision.Solution.Editor;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Input;

namespace ColorVision.Solution.Explorer
{
    [SolutionMenuContribution(priority: 235)]
    public sealed class ProjectMenuContribution : ISolutionMenuContribution
    {
        public string Id => "colorvision.solution.project-actions";
        public SolutionMenuSelectionPolicy SelectionPolicy => SolutionMenuSelectionPolicy.SingleOnly;

        public bool IsApplicable(SolutionMenuContext context)
        {
            return context.PrimaryNode is ProjectNode;
        }

        public IEnumerable<MenuItemMetadata> CreateMenuItems(SolutionMenuContext context)
        {
            var projectNode = (ProjectNode)context.PrimaryNode;
            var menuItems = new List<MenuItemMetadata>
            {
                new()
                {
                    GuidId = SolutionProjectCommands.EditProjectFileId,
                    Order = 3,
                    Header = "编辑项目文件(_E)",
                    Command = projectNode.EditProjectFileCommand,
                    Icon = MenuItemIcon.TryFindResource("DICode"),
                },
            };

            if (projectNode.Project.ItemRules?.Exclude.Count > 0)
            {
                menuItems.Add(new MenuItemMetadata
                {
                    GuidId = SolutionProjectCommands.ShowAllFilesId,
                    Order = 4,
                    Header = "显示所有文件(_S)",
                    Command = projectNode.ToggleShowAllFilesCommand,
                    IsChecked = projectNode.ShowAllFiles,
                });
            }

            if (SolutionFeatureVisibility.ShowBuildAndDebugUI
                && projectNode.SolutionExplorer?.CanSetStartupProject(projectNode.Project) == true)
            {
                menuItems.Add(new MenuItemMetadata
                {
                    GuidId = SolutionProjectCommands.SetStartupProjectId,
                    Order = 5,
                    Header = "设为启动项目(_A)",
                    Command = SolutionProjectCommands.SetStartupProject,
                    IsChecked = projectNode.IsStartupProject,
                });
            }

            foreach (ProjectCapabilityDescriptor capability in projectNode.Capabilities)
            {
                if (!SolutionFeatureVisibility.ShowBuildAndDebugUI
                    && SolutionFeatureVisibility.IsBuildOrDebugCapability(capability.Id))
                {
                    continue;
                }

                ICommand command = SolutionProjectCommands.GetCommand(capability.Id)
                    ?? new RelayCommand(
                        _ => projectNode.ExecuteCapability(capability.Id),
                        _ => projectNode.CanExecuteCapability(capability.Id));
                menuItems.Add(new MenuItemMetadata
                {
                    GuidId = $"ProjectCapability.{capability.Id}",
                    Order = 10 + capability.Order,
                    Header = capability.Header,
                    Command = command,
                    Icon = MenuItemIcon.TryFindResource(ProjectNode.GetCapabilityIcon(capability.Id)),
                });
            }
            return menuItems;
        }
    }

    [SolutionMenuContribution(priority: 238)]
    public sealed class SolutionRootMenuContribution : ISolutionMenuContribution
    {
        public string Id => "colorvision.solution.root-actions";
        public SolutionMenuSelectionPolicy SelectionPolicy => SolutionMenuSelectionPolicy.SingleOnly;

        public bool IsApplicable(SolutionMenuContext context)
        {
            return context.PrimaryNode is SolutionExplorer;
        }

        public IEnumerable<MenuItemMetadata> CreateMenuItems(SolutionMenuContext context)
        {
            var explorer = (SolutionExplorer)context.PrimaryNode;
            var menuItems = new List<MenuItemMetadata>();
            if (SolutionFeatureVisibility.ShowBuildAndDebugUI)
            {
                menuItems.AddRange(
                [
                    new MenuItemMetadata
                    {
                        GuidId = SolutionProjectCommands.BuildSolutionId,
                        Order = 5,
                        Header = "生成解决方案(_B)",
                        Command = SolutionProjectCommands.BuildSolution,
                        Icon = MenuItemIcon.TryFindResource("DIBuild"),
                    },
                    new MenuItemMetadata
                    {
                        GuidId = SolutionProjectCommands.RunStartupProjectId,
                        Order = 6,
                        Header = "运行启动项目(_R)",
                        Command = SolutionProjectCommands.Run,
                        Icon = MenuItemIcon.TryFindResource("DIRun"),
                        InputGestureText = "Ctrl+F5",
                    },
                    new MenuItemMetadata
                    {
                        GuidId = SolutionProjectCommands.DebugStartupProjectId,
                        Order = 7,
                        Header = "调试启动项目(_D)",
                        Command = SolutionProjectCommands.Debug,
                        Icon = MenuItemIcon.TryFindResource("DIDebug"),
                        InputGestureText = "F5",
                    },
                    new MenuItemMetadata
                    {
                        GuidId = SolutionProjectCommands.ActiveConfigurationId,
                        Order = 8,
                        Header = $"活动配置: {explorer.ActiveConfiguration}",
                    },
                ]);

                int configurationOrder = 0;
                foreach (string configuration in explorer.GetAvailableSolutionConfigurations())
                {
                    string selectedConfiguration = configuration;
                    menuItems.Add(new MenuItemMetadata
                    {
                        OwnerGuid = SolutionProjectCommands.ActiveConfigurationId,
                        GuidId = $"SolutionConfiguration.{configuration}",
                        Order = configurationOrder++,
                        Header = configuration,
                        Command = new RelayCommand(_ => explorer.SetActiveConfiguration(selectedConfiguration)),
                        IsChecked = string.Equals(
                            explorer.ActiveConfiguration,
                            configuration,
                            StringComparison.OrdinalIgnoreCase),
                    });
                }

                menuItems.Add(new MenuItemMetadata
                {
                    GuidId = SolutionProjectCommands.ActivePlatformId,
                    Order = 9,
                    Header = $"活动平台: {explorer.ActivePlatform}",
                });
                int platformOrder = 0;
                foreach (string platform in explorer.GetAvailableSolutionPlatforms())
                {
                    string selectedPlatform = platform;
                    menuItems.Add(new MenuItemMetadata
                    {
                        OwnerGuid = SolutionProjectCommands.ActivePlatformId,
                        GuidId = $"SolutionPlatform.{platform}",
                        Order = platformOrder++,
                        Header = platform,
                        Command = new RelayCommand(_ => explorer.SetActivePlatform(selectedPlatform)),
                        IsChecked = string.Equals(
                            explorer.ActivePlatform,
                            platform,
                            StringComparison.OrdinalIgnoreCase),
                    });
                }

                menuItems.Add(new MenuItemMetadata
                {
                    GuidId = SolutionProjectCommands.ConfigurationManagerId,
                    Order = 10,
                    Header = "配置管理器(_C)...",
                    Command = SolutionProjectCommands.ConfigurationManager,
                    Icon = MenuItemIcon.TryFindResource("DISetting"),
                });
            }
            menuItems.Add(new MenuItemMetadata
            {
                GuidId = "Edit",
                Order = 50,
                Header = ColorVision.Solution.Properties.Resources.EditSolution,
                Command = explorer.EditCommand,
            });
            return menuItems;
        }
    }

    [SolutionMenuContribution(priority: 225)]
    public sealed class SolutionOrganizationMenuContribution : ISolutionMenuContribution
    {
        public string Id => "colorvision.solution.organization";
        public SolutionMenuSelectionPolicy SelectionPolicy => SolutionMenuSelectionPolicy.SingleOnly;

        public bool IsApplicable(SolutionMenuContext context)
        {
            return context.PrimaryNode switch
            {
                ProjectNode projectNode => projectNode.SolutionExplorer?.IsExplicitProjectMode == true
                    && projectNode.SolutionExplorer.GetSolutionFolderOptions().Count > 1,
                SolutionFolderNode folderNode => folderNode.SolutionExplorer
                    .GetSolutionFolderMoveOptions(folderNode.FolderId).Count > 1,
                SolutionItemNode itemNode => itemNode.SolutionExplorer
                    .GetSolutionFolderOptions().Count > 1,
                _ => false,
            };
        }

        public IEnumerable<MenuItemMetadata> CreateMenuItems(SolutionMenuContext context)
        {
            return context.PrimaryNode switch
            {
                ProjectNode projectNode => CreateProjectMoveItems(projectNode),
                SolutionFolderNode folderNode => CreateSolutionFolderMoveItems(folderNode),
                SolutionItemNode itemNode => CreateSolutionItemMoveItems(itemNode),
                _ => [],
            };
        }

        private static List<MenuItemMetadata> CreateProjectMoveItems(ProjectNode node)
        {
            SolutionExplorer? explorer = node.SolutionExplorer;
            return explorer == null
                ? []
                : CreateMoveItems(
                    "MoveProjectToSolutionFolder",
                    80,
                    explorer.GetSolutionFolderOptions(),
                    explorer.GetProjectSolutionFolderId(node.Project),
                    targetFolderId => explorer.MoveProjectToSolutionFolder(node.Project, targetFolderId));
        }

        private static List<MenuItemMetadata> CreateSolutionFolderMoveItems(SolutionFolderNode node)
        {
            return CreateMoveItems(
                "MoveSolutionFolder",
                40,
                node.SolutionExplorer.GetSolutionFolderMoveOptions(node.FolderId),
                node.SolutionExplorer.GetSolutionFolderParentId(node.FolderId),
                node.MoveToSolutionFolder);
        }

        private static List<MenuItemMetadata> CreateSolutionItemMoveItems(SolutionItemNode node)
        {
            return CreateMoveItems(
                "MoveSolutionItem",
                80,
                node.SolutionExplorer.GetSolutionFolderOptions(),
                node.SolutionExplorer.GetSolutionItemFolderId(node.ItemId),
                targetFolderId => node.SolutionExplorer.MoveSolutionItemToFolder(node.ItemId, targetFolderId));
        }

        private static List<MenuItemMetadata> CreateMoveItems(
            string menuId,
            int menuOrder,
            IReadOnlyList<(string? Id, string DisplayName)> options,
            string? currentFolderId,
            Action<string?> move)
        {
            var menuItems = new List<MenuItemMetadata>
            {
                new()
                {
                    GuidId = menuId,
                    Order = menuOrder,
                    Header = "移动到解决方案文件夹(_M)",
                },
            };
            int order = 0;
            foreach (var option in options)
            {
                string? targetFolderId = option.Id;
                menuItems.Add(new MenuItemMetadata
                {
                    OwnerGuid = menuId,
                    GuidId = $"{menuId}.{targetFolderId ?? "Root"}",
                    Order = order++,
                    Header = option.DisplayName,
                    IsChecked = string.Equals(
                        currentFolderId,
                        targetFolderId,
                        StringComparison.OrdinalIgnoreCase),
                    Command = new RelayCommand(_ => move(targetFolderId)),
                });
            }
            return menuItems;
        }
    }

    [SolutionMenuContribution(priority: 230)]
    public sealed class ProjectItemMembershipMenuContribution : ISolutionMenuContribution
    {
        public string Id => "colorvision.solution.project-item-membership";
        public SolutionMenuSelectionPolicy SelectionPolicy => SolutionMenuSelectionPolicy.SingleOnly;

        public bool IsApplicable(SolutionMenuContext context)
        {
            return TryGetMembershipTarget(context.PrimaryNode, out _);
        }

        public IEnumerable<MenuItemMetadata> CreateMenuItems(SolutionMenuContext context)
        {
            if (!TryGetMembershipTarget(context.PrimaryNode, out ProjectNode? projectNode)
                || projectNode == null)
            {
                return [];
            }

            bool isIncluded = projectNode.IsPathIncludedByProjectRules(context.PrimaryNode.FullPath);
            return
            [
                new MenuItemMetadata
                {
                    GuidId = isIncluded
                        ? SolutionProjectCommands.ExcludeFromProjectId
                        : SolutionProjectCommands.IncludeInProjectId,
                    Order = 90,
                    Header = isIncluded ? "从项目中排除(_J)" : "包括在项目中(_J)",
                    Command = isIncluded
                        ? SolutionProjectCommands.ExcludeFromProject
                        : SolutionProjectCommands.IncludeInProject,
                },
            ];
        }

        private static bool TryGetMembershipTarget(
            SolutionNode node,
            out ProjectNode? projectNode)
        {
            projectNode = null;
            if (node is ProjectNode || string.IsNullOrWhiteSpace(node.FullPath))
                return false;
            projectNode = ProjectNode.FindOwningProject(node);
            return projectNode != null
                && ProjectProviderRegistry.CanChangeProjectItemMembership(
                    projectNode.Project,
                    node.FullPath);
        }
    }
}
