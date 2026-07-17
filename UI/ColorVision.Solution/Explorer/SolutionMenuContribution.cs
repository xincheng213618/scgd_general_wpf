using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Diagnostics;
using System.Reflection;

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
        public bool IsMultipleSelection => Nodes.Count > 1;

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
                if (!MatchesSelectionPolicy(contribution.SelectionPolicy, context.Nodes.Count))
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
