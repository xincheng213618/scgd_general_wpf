#pragma warning disable CA1720,CA1822,CA1854,CS8619
using System.Reflection;

namespace ColorVision.UI.Menus
{
    public partial class MenuManager
    {
        private MenuTypeSnapshot EnsureTypeCaches()
        {
            if (_typeSnapshot is { } snapshot) return snapshot;
            lock (_typeCacheLock)
            {
                if (_typeSnapshot is { } built) return built;

                int generation = Volatile.Read(ref _typeSnapshotGeneration);
                var itemTypes = new List<Type>();
                var providerTypes = new List<Type>();
                var attributeTypes = new List<Type>();
                foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
                {
                    foreach (var t in AssemblyHandler.GetInstance().GetTypes(assembly))
                    {
                        if (!IsConcreteMenuCandidate(t)) continue;

                        if (typeof(IMenuItem).IsAssignableFrom(t))
                        {
                            AddInstantiableType(itemTypes, t, nameof(IMenuItem));
                        }
                        else if (typeof(IMenuItemProvider).IsAssignableFrom(t))
                        {
                            AddInstantiableType(providerTypes, t, nameof(IMenuItemProvider));
                        }
                        else if (t.IsDefined(typeof(MenuItemAttribute), false))
                        {
                            AddInstantiableType(attributeTypes, t, nameof(MenuItemAttribute));
                        }
                    }
                }

                // A scan that raced with an assembly change is returned once but not cached.
                var result = new MenuTypeSnapshot(itemTypes.ToArray(), providerTypes.ToArray(), attributeTypes.ToArray());
                if (generation == Volatile.Read(ref _typeSnapshotGeneration))
                    _typeSnapshot = result;
                return result;
            }
        }

        private static bool IsConcreteMenuCandidate(Type? type)
        {
            return type != null
                && type.IsClass
                && !type.IsAbstract
                && !type.IsInterface
                && !type.ContainsGenericParameters
                && !type.IsDefined(typeof(ObsoleteAttribute), inherit: false);
        }

        private static bool HasPublicParameterlessConstructor(Type type)
        {
            return type.GetConstructor(Type.EmptyTypes) != null;
        }

        private void AddInstantiableType(List<Type> cache, Type type, string discoveryKind)
        {
            if (HasPublicParameterlessConstructor(type))
            {
                cache.Add(type);
                return;
            }

            if (log.IsDebugEnabled)
                log.Debug($"Skip {discoveryKind} discovery for {type.FullName}: no public parameterless constructor.");
        }

        /// <summary>
        /// 提供一个公共方法，获取系统当前所有过滤后的 IMenuItem（包含所有窗口）
        /// 用于之前的全局搜索或快捷键绑定功能
        /// </summary>
        public List<IMenuItem> GetAllMenuItemsFiltered()
        {
            return GetIMenuItemsFiltered(null);
        }

        private List<IMenuItem> GetWindowSpecificItems(string targetName, Func<Type, bool>? typeFilter)
        {
            var allItems = GetIMenuItemsFiltered(typeFilter);
            return allItems.Where(mi =>
                mi.TargetName == targetName ||
                mi.TargetName == MenuItemConstants.GlobalTarget).ToList();
        }

        private List<IMenuItem> GetIMenuItemsFiltered(Func<Type, bool>? typeFilter)
        {
            var allMenuItems = CreateAllMenuItems(typeFilter);
            var allFilteredItems = GetAllFilteredItems(allMenuItems);
            return allMenuItems.Where(mi => mi.GuidId == null || !allFilteredItems.Contains(CreateScopeKey(mi))).ToList();
        }

        private List<IMenuItem> CreateAllMenuItems(Func<Type, bool>? typeFilter)
        {
            var types = EnsureTypeCaches();
            var allMenuItems = new List<IMenuItem>(types.ItemTypes.Length + types.ProviderTypes.Length + types.AttributeTypes.Length + 16);

            foreach (var t in types.ItemTypes)
            {
                if (typeFilter != null && !typeFilter(t))
                    continue;

                try
                {
                    if (Activator.CreateInstance(t) is IMenuItem item && item.Command != null)
                        allMenuItems.Add(item);
                }
                catch (Exception ex)
                {
                    log.Warn($"Create IMenuItem failed: {t.FullName}: {ex.Message}");
                }
            }

            foreach (var t in types.ProviderTypes)
            {
                if (typeFilter != null && !typeFilter(t))
                    continue;

                try
                {
                    if (Activator.CreateInstance(t) is IMenuItemProvider provider)
                    {
                        var provided = provider.GetMenuItems();
                        if (provided != null)
                        {
                            foreach (var p in provided)
                            {
                                if (p != null && p.Command != null && p.Header != null)
                                    allMenuItems.Add(p);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Warn($"Create IMenuItemProvider failed: {t.FullName}: {ex.Message}");
                }
            }

            foreach (var t in types.AttributeTypes)
            {
                if (typeFilter != null && !typeFilter(t))
                    continue;

                var attr = t.GetCustomAttribute<MenuItemAttribute>(false);
                if (attr?.Header != null)
                    allMenuItems.Add(new LazyMenuItemAdapter(attr, t));
            }

            return allMenuItems;
        }

        private HashSet<MenuItemScopeKey> GetAllFilteredItems(IReadOnlyCollection<IMenuItem> allMenuItems)
        {
            var result = allMenuItems
                .Where(item => item.GuidId != null
                    && (FilteredGuids.Contains(item.GuidId) || ScopedFilteredItems.Contains(CreateScopeKey(item))))
                .Select(CreateScopeKey)
                .ToHashSet();

            bool added;
            do
            {
                added = false;
                foreach (var item in allMenuItems)
                {
                    if (item.GuidId == null)
                        continue;

                    var effectiveOwner = GetEffectiveOwnerGuid(item);
                    if (effectiveOwner != null && IsFilteredOwner(item.TargetName, effectiveOwner, result) && result.Add(CreateScopeKey(item)))
                        added = true;
                }
            } while (added);

            return result;
        }

        private static bool IsFilteredOwner(string targetName, string ownerGuid, HashSet<MenuItemScopeKey> filteredItems)
        {
            return filteredItems.Contains(new MenuItemScopeKey(targetName, ownerGuid))
                || filteredItems.Contains(new MenuItemScopeKey(MenuItemConstants.GlobalTarget, ownerGuid));
        }

        private static MenuItemScopeKey CreateScopeKey(IMenuItem item)
        {
            return new MenuItemScopeKey(item.TargetName, item.GuidId!);
        }

        /// <summary>
        /// 获取系统中所有加载的菜单项（不应用任何过滤或隐藏规则）
        /// 主要供配置界面使用，这样即便是被隐藏的菜单也能在设置里被找回来
        /// </summary>
        public List<IMenuItem> GetAllMenuItems()
        {
            return CreateAllMenuItems(null);
        }
    }
}
