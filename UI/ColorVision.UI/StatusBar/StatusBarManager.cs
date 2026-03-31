using ColorVision.Common.Utilities;
using ColorVision.UI.Menus;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI
{
    public class StatusBarManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(StatusBarManager));
        private static StatusBarManager _instance;
        private static readonly object _locker = new();
        public static StatusBarManager GetInstance() { lock (_locker) { return _instance ??= new StatusBarManager(); } }

        private readonly List<StatusBarMeta> _globalItems = new();
        private readonly List<StatusBarMeta> _activeDocumentItems = new();
        private readonly Dictionary<string, StatusBarControl> _controls = new();
        private readonly List<IStatusBarProvider> _providers = new();
        private readonly Dictionary<IStatusBarProvider, List<StatusBarMeta>> _providerItems = new();
        private bool _assembled;
        private IActiveDocumentStatusProvider _currentDocumentProvider;

        private StatusBarManager() { }

        /// <summary>
        /// 在指定容器（Grid）中初始化状态栏，自动创建 StatusBarControl。
        /// 页面上只需放一个 Grid 占位符即可。
        /// </summary>
        /// <param name="container">宿主 Grid 控件</param>
        /// <param name="targetName">窗口标识，用于过滤 IStatusBarProvider 提供的项</param>
        public StatusBarControl Init(Grid container, string targetName)
        {
            container.Children.Clear();
            var control = new StatusBarControl { TargetName = targetName };
            control.SetValue(Grid.RowProperty, 0);
            container.Children.Add(control);

            _controls[targetName] = control;

            if (!_assembled)
            {
                LoadFromAssemblies();
                _assembled = true;
            }

            RefreshControl(control, targetName);
            return control;
        }

        /// <summary>
        /// 通知活动文档已发生切换，更新上下文状态栏项。
        /// 如果新的活动内容实现了 IActiveDocumentStatusProvider，则显示其状态栏项；
        /// 否则清除上一个文档的状态栏项。
        /// </summary>
        public void OnActiveDocumentChanged(object activeContent)
        {
            // 取消订阅旧 provider 的事件
            if (_currentDocumentProvider != null)
                _currentDocumentProvider.StatusBarItemsChanged -= OnCurrentProviderItemsChanged;

            // 移除旧的活动文档状态项
            foreach (var meta in _activeDocumentItems)
            {
                var id = meta.Id ?? meta.Name;
                if (id != null)
                {
                    foreach (var kvp in _controls)
                        kvp.Value.RemoveStatusBarItem(id);
                }
            }
            _activeDocumentItems.Clear();
            _currentDocumentProvider = null;

            // 添加新的活动文档状态项
            if (activeContent is IActiveDocumentStatusProvider provider)
            {
                _currentDocumentProvider = provider;
                provider.StatusBarItemsChanged += OnCurrentProviderItemsChanged;
                ApplyActiveDocumentItems(provider);
            }
        }

        /// <summary>
        /// 当前活动文档的状态栏项发生变化时（如异步加载完成），重新获取并刷新。
        /// </summary>
        private void OnCurrentProviderItemsChanged(object? sender, EventArgs e)
        {
            if (sender != _currentDocumentProvider) return;

            Application.Current?.Dispatcher.Invoke(() =>
            {
                // 移除旧的活动文档项
                foreach (var meta in _activeDocumentItems)
                {
                    var id = meta.Id ?? meta.Name;
                    if (id != null)
                    {
                        foreach (var kvp in _controls)
                            kvp.Value.RemoveStatusBarItem(id);
                    }
                }
                _activeDocumentItems.Clear();

                // 重新获取并添加
                ApplyActiveDocumentItems(_currentDocumentProvider);
            });
        }

        /// <summary>
        /// 从 provider 获取状态栏项并添加到所有匹配的控件中
        /// </summary>
        private void ApplyActiveDocumentItems(IActiveDocumentStatusProvider provider)
        {
            var items = provider.GetActiveStatusBarItems()?.ToList();
            if (items != null)
            {
                _activeDocumentItems.AddRange(items);
                foreach (var item in items)
                {
                    foreach (var kvp in _controls)
                    {
                        if (item.TargetName == MenuItemConstants.GlobalTarget || item.TargetName == kvp.Key)
                            kvp.Value.AddStatusBarItem(item);
                    }
                }
            }
        }

        /// <summary>
        /// 运行时动态添加全局状态栏项
        /// </summary>
        public void AddItem(StatusBarMeta item)
        {
            _globalItems.Add(item);
            foreach (var kvp in _controls)
            {
                if (item.TargetName == MenuItemConstants.GlobalTarget || item.TargetName == kvp.Key)
                    kvp.Value.AddStatusBarItem(item);
            }
        }

        /// <summary>
        /// 运行时动态移除全局状态栏项
        /// </summary>
        public void RemoveItem(string id)
        {
            _globalItems.RemoveAll(i => (i.Id ?? i.Name) == id);
            foreach (var kvp in _controls)
                kvp.Value.RemoveStatusBarItem(id);
        }

        /// <summary>
        /// 刷新所有已注册的控件
        /// </summary>
        public void RefreshAll()
        {
            LoadFromAssemblies();
            foreach (var kvp in _controls)
                RefreshControl(kvp.Value, kvp.Key);
        }

        /// <summary>
        /// 从程序集扫描所有 IStatusBarProvider 实现，保留实例以支持动态刷新
        /// </summary>
        private void LoadFromAssemblies()
        {
            _globalItems.Clear();

            if (_providers.Count == 0)
            {
                // 首次加载：创建实例并订阅事件
                foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
                {
                    foreach (var type in assembly.GetTypes()
                        .Where(t => typeof(IStatusBarProvider).IsAssignableFrom(t) && !t.IsAbstract))
                    {
                        try
                        {
                            if (Activator.CreateInstance(type) is IStatusBarProvider provider)
                            {
                                _providers.Add(provider);

                                if (provider is IStatusBarProviderUpdatable updatable)
                                {
                                    updatable.StatusBarItemsChanged += OnProviderItemsChanged;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            log.Error($"Failed to load StatusBarProvider {type.Name}: {ex.Message}");
                        }
                    }
                }
            }

            // 从所有已缓存的 provider 获取 metadata
            _providerItems.Clear();
            foreach (var provider in _providers)
            {
                try
                {
                    var metas = provider.GetStatusBarIconMetadata()?.ToList();
                    if (metas != null && metas.Count > 0)
                    {
                        _providerItems[provider] = metas;
                        _globalItems.AddRange(metas);
                    }
                }
                catch (Exception ex)
                {
                    log.Error($"Failed to get metadata from {provider.GetType().Name}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 当可刷新的 provider 状态变化时，重新获取其 metadata 并刷新控件
        /// </summary>
        private void OnProviderItemsChanged(object? sender, EventArgs e)
        {
            if (sender is not IStatusBarProvider provider) return;

            Application.Current?.Dispatcher.Invoke(() =>
            {
                // 移除该 provider 旧的项
                if (_providerItems.TryGetValue(provider, out var oldItems))
                {
                    foreach (var meta in oldItems)
                    {
                        var id = meta.Id ?? meta.Name;
                        if (id != null)
                        {
                            _globalItems.RemoveAll(m => (m.Id ?? m.Name) == id);
                            foreach (var kvp in _controls)
                                kvp.Value.RemoveStatusBarItem(id);
                        }
                    }
                }

                // 获取新的项
                try
                {
                    var newItems = provider.GetStatusBarIconMetadata()?.ToList();
                    if (newItems != null && newItems.Count > 0)
                    {
                        _providerItems[provider] = newItems;
                        _globalItems.AddRange(newItems);
                        foreach (var item in newItems)
                        {
                            foreach (var kvp in _controls)
                            {
                                if (item.TargetName == MenuItemConstants.GlobalTarget || item.TargetName == kvp.Key)
                                    kvp.Value.AddStatusBarItem(item);
                            }
                        }
                    }
                    else
                    {
                        _providerItems.Remove(provider);
                    }
                }
                catch (Exception ex)
                {
                    log.Error($"Failed to refresh provider {provider.GetType().Name}: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 刷新指定控件，过滤出匹配 TargetName 或 Global 的项目，
        /// 同时包含当前活动文档的上下文项。
        /// </summary>
        private void RefreshControl(StatusBarControl control, string targetName)
        {
            var items = _globalItems
                .Concat(_activeDocumentItems)
                .Where(i => i.TargetName == MenuItemConstants.GlobalTarget || i.TargetName == targetName)
                .ToList();

            control.LoadItems(items);
        }
    }
}
