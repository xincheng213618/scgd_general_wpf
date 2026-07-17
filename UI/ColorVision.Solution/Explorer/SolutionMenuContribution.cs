using ColorVision.Common.MVVM;
using ColorVision.Solution.Editor;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Input;

namespace ColorVision.Solution.Explorer
{
    public enum SolutionMenuSelectionPolicy
    {
        SingleOnly,
        MultipleOnly,
        Any,
    }

    /// <summary>
    /// Immutable selection snapshot used while one explorer context menu is open.
    /// Visual nodes preserve search-result identity; Nodes are resolved command targets.
    /// </summary>
    public sealed class SolutionMenuContext
    {
        public IReadOnlyList<SolutionNode> VisualNodes { get; }
        public IReadOnlyList<SolutionNode> Nodes { get; }
        public SolutionNode PrimaryVisualNode => VisualNodes[0];
        public SolutionNode PrimaryNode => Nodes[0];
        public bool IsMultipleSelection => VisualNodes.Count > 1;

        public SolutionMenuContext(IReadOnlyList<SolutionNode> visualNodes)
        {
            ArgumentNullException.ThrowIfNull(visualNodes);
            VisualNodes = visualNodes.Where(node => node != null).Distinct().ToList().AsReadOnly();
            Nodes = VisualNodes
                .Select(node => node.ResolveCommandTarget())
                .Distinct()
                .ToList()
                .AsReadOnly();
            if (Nodes.Count == 0)
                throw new ArgumentException("菜单上下文必须包含至少一个节点。", nameof(visualNodes));
        }
    }

    public interface ISolutionMenuContribution
    {
        string Id { get; }
        SolutionMenuSelectionPolicy SelectionPolicy { get; }
        bool IsApplicable(SolutionMenuContext context);
        IEnumerable<MenuItemMetadata> CreateMenuItems(SolutionMenuContext context);
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class SolutionMenuContributionAttribute : Attribute
    {
        public int Priority { get; }

        public SolutionMenuContributionAttribute(int priority = 0)
        {
            Priority = priority;
        }
    }

    /// <summary>
    /// Composable explorer-menu extension point. Contributions are evaluated on
    /// every menu opening, so plugin state changes do not require node recreation.
    /// </summary>
    public static class SolutionMenuContributionRegistry
    {
        private sealed record Registration(ISolutionMenuContribution Contribution, int Priority);

        private static readonly List<Registration> _contributions = new();
        private static readonly HashSet<Assembly> _registeredAssemblies = new();
        private static readonly object _syncRoot = new();
        private static bool _initialized;
        private static bool _assemblyLoadSubscribed;

        public static event EventHandler? ContributionsChanged;

        public static void Initialize()
        {
            if (_initialized)
                return;

            bool changed = false;
            lock (_syncRoot)
            {
                if (_initialized)
                    return;
                if (!_assemblyLoadSubscribed)
                {
                    AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_AssemblyLoad;
                    _assemblyLoadSubscribed = true;
                }

                Assembly[] assemblies = AssemblyService.Instance?.GetAssemblies()
                    ?? AppDomain.CurrentDomain.GetAssemblies();
                foreach (Assembly assembly in assemblies)
                    changed |= RegisterContributionsFromAssemblyCore(assembly);
                _initialized = true;
            }

            if (changed)
                ContributionsChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void Register(ISolutionMenuContribution contribution, int priority = 0)
        {
            ArgumentNullException.ThrowIfNull(contribution);
            Initialize();
            ValidateContribution(contribution);
            lock (_syncRoot)
                RegisterCore(contribution, priority, replaceExisting: true);
            ContributionsChanged?.Invoke(null, EventArgs.Empty);
        }

        public static bool Unregister(string contributionId)
        {
            if (string.IsNullOrWhiteSpace(contributionId))
                return false;
            Initialize();
            bool changed;
            lock (_syncRoot)
                changed = _contributions.RemoveAll(item => string.Equals(
                    item.Contribution.Id,
                    contributionId,
                    StringComparison.OrdinalIgnoreCase)) > 0;
            if (changed)
                ContributionsChanged?.Invoke(null, EventArgs.Empty);
            return changed;
        }

        public static IReadOnlyList<MenuItemMetadata> GetMenuItems(SolutionMenuContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            Initialize();
            Registration[] snapshot;
            lock (_syncRoot)
                snapshot = _contributions.ToArray();

            var menuItems = new List<MenuItemMetadata>();
            var registeredMenuIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Registration registration in snapshot)
            {
                ISolutionMenuContribution contribution = registration.Contribution;
                if (!MatchesSelectionPolicy(contribution.SelectionPolicy, context.VisualNodes.Count))
                    continue;
                try
                {
                    if (!contribution.IsApplicable(context))
                        continue;
                    IEnumerable<MenuItemMetadata>? createdItems = contribution.CreateMenuItems(context);
                    if (createdItems == null)
                        continue;
                    foreach (MenuItemMetadata item in createdItems.Where(item => item != null
                        && !string.IsNullOrWhiteSpace(item.GuidId)
                        && !string.Equals(item.GuidId, Guid.Empty.ToString(), StringComparison.OrdinalIgnoreCase)))
                    {
                        if (registeredMenuIds.Add(item.GuidId!))
                            menuItems.Add(item);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"加载解决方案菜单贡献失败: {contribution.Id}, {ex}");
                }
            }
            return menuItems;
        }

        private static void CurrentDomain_AssemblyLoad(object? sender, AssemblyLoadEventArgs e)
        {
            bool changed;
            lock (_syncRoot)
                changed = RegisterContributionsFromAssemblyCore(e.LoadedAssembly);
            if (changed)
                ContributionsChanged?.Invoke(null, EventArgs.Empty);
        }

        private static bool RegisterContributionsFromAssemblyCore(Assembly assembly)
        {
            if (!_registeredAssemblies.Add(assembly))
                return false;

            bool changed = false;
            foreach (Type type in GetLoadableTypes(assembly))
            {
                var attribute = type.GetCustomAttribute<SolutionMenuContributionAttribute>();
                if (attribute == null
                    || !typeof(ISolutionMenuContribution).IsAssignableFrom(type)
                    || type.IsInterface
                    || type.IsAbstract)
                {
                    continue;
                }

                try
                {
                    var contribution = (ISolutionMenuContribution)Activator.CreateInstance(type)!;
                    ValidateContribution(contribution);
                    changed |= RegisterCore(contribution, attribute.Priority, replaceExisting: false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"注册解决方案菜单贡献失败: {type.FullName}, {ex}");
                }
            }
            return changed;
        }

        private static bool RegisterCore(
            ISolutionMenuContribution contribution,
            int priority,
            bool replaceExisting)
        {
            int existingIndex = _contributions.FindIndex(item => string.Equals(
                item.Contribution.Id,
                contribution.Id,
                StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                if (!replaceExisting && _contributions[existingIndex].Priority >= priority)
                    return false;
                _contributions.RemoveAt(existingIndex);
            }
            _contributions.Add(new Registration(contribution, priority));
            _contributions.Sort((left, right) =>
            {
                int result = right.Priority.CompareTo(left.Priority);
                return result != 0
                    ? result
                    : StringComparer.OrdinalIgnoreCase.Compare(
                        left.Contribution.Id,
                        right.Contribution.Id);
            });
            return true;
        }

        private static bool MatchesSelectionPolicy(
            SolutionMenuSelectionPolicy selectionPolicy,
            int selectedNodeCount)
        {
            return selectionPolicy switch
            {
                SolutionMenuSelectionPolicy.SingleOnly => selectedNodeCount == 1,
                SolutionMenuSelectionPolicy.MultipleOnly => selectedNodeCount > 1,
                SolutionMenuSelectionPolicy.Any => selectedNodeCount > 0,
                _ => false,
            };
        }

        private static void ValidateContribution(ISolutionMenuContribution contribution)
        {
            if (string.IsNullOrWhiteSpace(contribution.Id))
                throw new ArgumentException("菜单贡献 Id 不允许为空。", nameof(contribution));
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.OfType<Type>();
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }
    }

    [SolutionMenuContribution(priority: 300)]
    public sealed class ResourceOpenMenuContribution : ISolutionMenuContribution
    {
        public string Id => "colorvision.solution.resource-open";
        public SolutionMenuSelectionPolicy SelectionPolicy => SolutionMenuSelectionPolicy.Any;

        public bool IsApplicable(SolutionMenuContext context)
        {
            if (!context.IsMultipleSelection)
            {
                return context.PrimaryNode.CanOpen
                    || !string.IsNullOrWhiteSpace(context.PrimaryNode.EditorResourcePath);
            }
            return SolutionResourceOpenPolicy.CanOpen(context.Nodes);
        }

        public IEnumerable<MenuItemMetadata> CreateMenuItems(SolutionMenuContext context)
        {
            SolutionNode node = context.PrimaryNode;
            var menuItems = new List<MenuItemMetadata>();
            bool canOpen = context.IsMultipleSelection
                ? SolutionResourceOpenPolicy.CanOpen(context.Nodes)
                : node.CanOpen;
            if (canOpen)
            {
                menuItems.Add(new MenuItemMetadata
                {
                    GuidId = SolutionResourceCommands.OpenId,
                    Order = 1,
                    Header = ColorVision.Solution.Properties.Resources.MenuOpen,
                    Icon = MenuItemIcon.TryFindResource("DIOpen"),
                    InputGestureText = "Enter",
                    Command = SolutionResourceCommands.Open,
                });
            }

            if (!context.IsMultipleSelection
                && node.EditorResourcePath is { } resourcePath
                && ResourceOpenService.Instance.GetOpenWithEditors(resourcePath).Count > 0)
            {
                menuItems.Add(new MenuItemMetadata
                {
                    GuidId = SolutionResourceCommands.OpenWithId,
                    Order = 2,
                    Header = $"{ColorVision.Solution.Properties.Resources.Sol_OpenAs}(_N)",
                    Command = SolutionResourceCommands.OpenWith,
                });
            }
            return menuItems;
        }
    }

    [SolutionMenuContribution(priority: 250)]
    public sealed class NodeManagementMenuContribution : ISolutionMenuContribution
    {
        public string Id => "colorvision.solution.node-management";
        public SolutionMenuSelectionPolicy SelectionPolicy => SolutionMenuSelectionPolicy.SingleOnly;

        public bool IsApplicable(SolutionMenuContext context)
        {
            SolutionNode node = context.PrimaryNode;
            return node.CanRefresh || node.CanReName || node.CanShowProperties;
        }

        public IEnumerable<MenuItemMetadata> CreateMenuItems(SolutionMenuContext context)
        {
            SolutionNode node = context.PrimaryNode;
            var menuItems = new List<MenuItemMetadata>();
            if (node.CanRefresh)
            {
                menuItems.Add(new MenuItemMetadata
                {
                    GuidId = SolutionCommandIds.Refresh,
                    Order = 3,
                    Header = ColorVision.Solution.Properties.Resources.Refresh,
                    Command = NavigationCommands.Refresh,
                });
            }
            if (node.CanReName)
            {
                menuItems.Add(new MenuItemMetadata
                {
                    GuidId = SolutionCommandIds.Rename,
                    Order = 104,
                    Header = ColorVision.UI.Properties.Resources.MenuRename,
                    Icon = MenuItemIcon.TryFindResource("DIRename"),
                    InputGestureText = "F2",
                    Command = Commands.ReName,
                });
            }
            if (node.CanShowProperties)
            {
                menuItems.Add(new MenuItemMetadata
                {
                    GuidId = SolutionCommandIds.Properties,
                    Order = 9999,
                    Header = ColorVision.Solution.Properties.Resources.MenuProperty,
                    Icon = MenuItemIcon.TryFindResource("DIProperty"),
                    Command = ApplicationCommands.Properties,
                });
            }
            return menuItems;
        }
    }

    [SolutionMenuContribution(priority: 240)]
    public sealed class ContainerMenuContribution : ISolutionMenuContribution
    {
        public string Id => "colorvision.solution.container-actions";
        public SolutionMenuSelectionPolicy SelectionPolicy => SolutionMenuSelectionPolicy.SingleOnly;

        public bool IsApplicable(SolutionMenuContext context)
        {
            return context.PrimaryNode.CanAdd
                && context.PrimaryNode is ISolutionContainerNode container
                && container.SupportedContainerActions != SolutionContainerAction.None;
        }

        public IEnumerable<MenuItemMetadata> CreateMenuItems(SolutionMenuContext context)
        {
            var container = (ISolutionContainerNode)context.PrimaryNode;
            var menuItems = new List<MenuItemMetadata>
            {
                new()
                {
                    GuidId = SolutionContainerCommands.AddMenuId,
                    Order = 10,
                    Header = ColorVision.Solution.Properties.Resources.MenuAdd,
                },
            };
            AddAction(
                menuItems,
                container,
                SolutionContainerAction.AddNewItem,
                SolutionContainerCommands.AddNewItemId,
                1,
                "新建项(_N)...",
                SolutionContainerCommands.AddNewItem);
            AddAction(
                menuItems,
                container,
                SolutionContainerAction.AddExistingItem,
                SolutionContainerCommands.AddExistingItemId,
                2,
                "现有项(_E)...",
                SolutionContainerCommands.AddExistingItem);
            AddAction(
                menuItems,
                container,
                SolutionContainerAction.CreateFolder,
                SolutionContainerCommands.CreateFolderId,
                10,
                ColorVision.Solution.Properties.Resources.AddFolder,
                SolutionContainerCommands.CreateFolder);
            AddAction(
                menuItems,
                container,
                SolutionContainerAction.AddNewProject,
                SolutionContainerCommands.AddNewProjectId,
                15,
                "新建项目(_P)...",
                SolutionContainerCommands.AddNewProject);
            AddAction(
                menuItems,
                container,
                SolutionContainerAction.AddExistingProject,
                SolutionContainerCommands.AddExistingProjectId,
                20,
                "现有项目(_E)...",
                SolutionContainerCommands.AddExistingProject);
            AddAction(
                menuItems,
                container,
                SolutionContainerAction.CreateSolutionFolder,
                SolutionContainerCommands.CreateSolutionFolderId,
                25,
                "新建解决方案文件夹(_F)",
                SolutionContainerCommands.CreateSolutionFolder);
            return menuItems;
        }

        private static void AddAction(
            List<MenuItemMetadata> menuItems,
            ISolutionContainerNode container,
            SolutionContainerAction action,
            string id,
            int order,
            string header,
            ICommand command)
        {
            if (!container.Supports(action))
                return;
            menuItems.Add(new MenuItemMetadata
            {
                OwnerGuid = SolutionContainerCommands.AddMenuId,
                GuidId = id,
                Order = order,
                Header = header,
                Command = command,
            });
        }
    }

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

            if (projectNode.SolutionExplorer?.CanSetStartupProject(projectNode.Project) == true)
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
            var menuItems = new List<MenuItemMetadata>
            {
                new()
                {
                    GuidId = SolutionProjectCommands.BuildSolutionId,
                    Order = 5,
                    Header = "生成解决方案(_B)",
                    Command = SolutionProjectCommands.BuildSolution,
                    Icon = MenuItemIcon.TryFindResource("DIBuild"),
                },
                new()
                {
                    GuidId = SolutionProjectCommands.RunStartupProjectId,
                    Order = 6,
                    Header = "运行启动项目(_R)",
                    Command = SolutionProjectCommands.Run,
                    Icon = MenuItemIcon.TryFindResource("DIRun"),
                    InputGestureText = "Ctrl+F5",
                },
                new()
                {
                    GuidId = SolutionProjectCommands.DebugStartupProjectId,
                    Order = 7,
                    Header = "调试启动项目(_D)",
                    Command = SolutionProjectCommands.Debug,
                    Icon = MenuItemIcon.TryFindResource("DIDebug"),
                    InputGestureText = "F5",
                },
                new()
                {
                    GuidId = SolutionProjectCommands.ActiveConfigurationId,
                    Order = 8,
                    Header = $"活动配置: {explorer.ActiveConfiguration}",
                },
            };

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
                GuidId = SolutionProjectCommands.ConfigurationManagerId,
                Order = 9,
                Header = "配置管理器(_C)...",
                Command = SolutionProjectCommands.ConfigurationManager,
                Icon = MenuItemIcon.TryFindResource("DISetting"),
            });
            if (explorer.CanModifySolutionStructure)
            {
                menuItems.Add(new MenuItemMetadata
                {
                    GuidId = "Edit",
                    Order = 50,
                    Header = ColorVision.Solution.Properties.Resources.EditSolution,
                    Command = explorer.EditCommand,
                });
            }
            else if (explorer.ImportedSolutionSourcePath is { } sourcePath)
            {
                menuItems.Add(new MenuItemMetadata
                {
                    GuidId = SolutionResourceCommands.ImportedSourceMenuId,
                    Order = 40,
                    Header = $"外部解决方案源: {Path.GetFileName(sourcePath)}",
                });
                menuItems.Add(new MenuItemMetadata
                {
                    OwnerGuid = SolutionResourceCommands.ImportedSourceMenuId,
                    GuidId = SolutionResourceCommands.EditImportedSourceId,
                    Order = 1,
                    Header = "编辑源文件(_E)",
                    Command = explorer.OpenImportedSolutionSourceCommand,
                    Icon = MenuItemIcon.TryFindResource("DICode"),
                });
                menuItems.Add(new MenuItemMetadata
                {
                    OwnerGuid = SolutionResourceCommands.ImportedSourceMenuId,
                    GuidId = SolutionResourceCommands.RevealImportedSourceId,
                    Order = 2,
                    Header = "在文件资源管理器中定位(_X)",
                    Command = explorer.RevealImportedSolutionSourceCommand,
                });
                menuItems.Add(new MenuItemMetadata
                {
                    OwnerGuid = SolutionResourceCommands.ImportedSourceMenuId,
                    GuidId = SolutionResourceCommands.CopyImportedSourcePathId,
                    Order = 3,
                    Header = "复制源文件路径(_C)",
                    Command = explorer.CopyImportedSolutionSourcePathCommand,
                });
            }
            menuItems.Add(new MenuItemMetadata
            {
                GuidId = "MenuOpenFileInExplorer",
                Order = 200,
                Header = ColorVision.Solution.Properties.Resources.MenuOpenFileInExplorer,
                Command = explorer.OpenFileInExplorerCommand,
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
                    && projectNode.SolutionExplorer.CanModifySolutionStructure
                    && projectNode.SolutionExplorer.GetSolutionFolderOptions().Count > 1,
                SolutionFolderNode folderNode => folderNode.SolutionExplorer.CanModifySolutionStructure
                    && folderNode.SolutionExplorer
                    .GetSolutionFolderMoveOptions(folderNode.FolderId).Count > 1,
                SolutionItemNode itemNode => itemNode.SolutionExplorer.CanModifySolutionStructure
                    && itemNode.SolutionExplorer
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

    [SolutionMenuContribution(priority: 210)]
    public sealed class DeleteMenuContribution : ISolutionMenuContribution
    {
        public string Id => "colorvision.solution.delete";
        public SolutionMenuSelectionPolicy SelectionPolicy => SolutionMenuSelectionPolicy.Any;

        public bool IsApplicable(SolutionMenuContext context)
        {
            return context.Nodes.Count > 0 && context.Nodes.All(node => node.CanDelete);
        }

        public IEnumerable<MenuItemMetadata> CreateMenuItems(SolutionMenuContext context)
        {
            string header = context.Nodes.Count == 1
                ? GetDeleteHeader(context.PrimaryNode.DeleteKind)
                : ColorVision.UI.Properties.Resources.MenuDelete;
            return
            [
                new MenuItemMetadata
                {
                    GuidId = SolutionCommandIds.Delete,
                    Order = 103,
                    Command = ApplicationCommands.Delete,
                    Header = header,
                    Icon = MenuItemIcon.TryFindResource("DIDelete"),
                    InputGestureText = "Del",
                },
            ];
        }

        private static string GetDeleteHeader(SolutionDeleteKind deleteKind)
        {
            return deleteKind switch
            {
                SolutionDeleteKind.RemoveFromSolution => "从解决方案中移除(_V)",
                SolutionDeleteKind.RemoveSolutionFolder => "移除解决方案文件夹(_V)",
                _ => ColorVision.UI.Properties.Resources.MenuDelete,
            };
        }
    }

    [SolutionMenuContribution(priority: 200)]
    public sealed class ClipboardMenuContribution : ISolutionMenuContribution
    {
        public string Id => "colorvision.solution.clipboard";
        public SolutionMenuSelectionPolicy SelectionPolicy => SolutionMenuSelectionPolicy.Any;

        public bool IsApplicable(SolutionMenuContext context)
        {
            return CanCopy(context.Nodes)
                || CanCut(context.Nodes)
                || (!context.IsMultipleSelection && context.PrimaryNode.CanPaste);
        }

        public IEnumerable<MenuItemMetadata> CreateMenuItems(SolutionMenuContext context)
        {
            var menuItems = new List<MenuItemMetadata>();
            if (CanCut(context.Nodes))
            {
                menuItems.Add(new MenuItemMetadata
                {
                    GuidId = SolutionCommandIds.Cut,
                    Order = 100,
                    Command = ApplicationCommands.Cut,
                    Header = ColorVision.UI.Properties.Resources.MenuCut,
                    Icon = MenuItemIcon.TryFindResource("DICut"),
                    InputGestureText = "Ctrl+X",
                });
            }
            if (CanCopy(context.Nodes))
            {
                menuItems.Add(new MenuItemMetadata
                {
                    GuidId = SolutionCommandIds.Copy,
                    Order = 101,
                    Command = ApplicationCommands.Copy,
                    Header = ColorVision.UI.Properties.Resources.MenuCopy,
                    Icon = MenuItemIcon.TryFindResource("DICopy"),
                    InputGestureText = "Ctrl+C",
                });
            }
            if (!context.IsMultipleSelection && context.PrimaryNode.CanPaste)
            {
                menuItems.Add(new MenuItemMetadata
                {
                    GuidId = SolutionCommandIds.Paste,
                    Order = 102,
                    Command = ApplicationCommands.Paste,
                    Header = ColorVision.UI.Properties.Resources.MenuPaste,
                    Icon = MenuItemIcon.TryFindResource("DIPaste"),
                    InputGestureText = "Ctrl+V",
                });
            }
            return menuItems;
        }

        private static bool CanCopy(IReadOnlyList<SolutionNode> nodes)
        {
            return nodes.Count > 0
                && nodes.All(node => node.CanCopy && node.ClipboardResourcePath != null);
        }

        private static bool CanCut(IReadOnlyList<SolutionNode> nodes)
        {
            return nodes.Count > 0
                && nodes.All(node => node.CanCut && node.ClipboardResourcePath != null);
        }
    }

    [SolutionMenuContribution(priority: 100)]
    public sealed class CopyFullPathMenuContribution : ISolutionMenuContribution
    {
        public string Id => "colorvision.solution.copy-full-path";
        public SolutionMenuSelectionPolicy SelectionPolicy => SolutionMenuSelectionPolicy.SingleOnly;

        public bool IsApplicable(SolutionMenuContext context)
        {
            return !string.IsNullOrWhiteSpace(context.PrimaryNode.FullPath);
        }

        public IEnumerable<MenuItemMetadata> CreateMenuItems(SolutionMenuContext context)
        {
            SolutionNode node = context.PrimaryNode;
            return
            [
                new MenuItemMetadata
                {
                    GuidId = "CopyFullPath",
                    Order = 200,
                    Header = ColorVision.Solution.Properties.Resources.CopyFullPath,
                    Icon = MenuItemIcon.TryFindResource("DICopyFullPath"),
                    Command = new RelayCommand(_ => node.CopyFullPath()),
                },
            ];
        }
    }
}
