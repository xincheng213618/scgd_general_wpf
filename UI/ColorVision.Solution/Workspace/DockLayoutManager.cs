using AvalonDock;
using AvalonDock.Layout;
using AvalonDock.Layout.Serialization;
using ColorVision.UI.Views;
using log4net;
using System.IO;
using System.Windows;

namespace ColorVision.Solution.Workspace
{
    /// <summary>
    /// 工作区停靠布局管理器，负责 AvalonDock 布局的持久化、重置和面板可见性管理。
    /// 维护持久化内容注册表，确保面板内容在关闭/隐藏/重置后不丢失。
    /// 相比 Spectrum 简化版，增加了面板元数据、动态注册和更灵活的默认布局构建。
    /// </summary>
    public class DockLayoutManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DockLayoutManager));

        private const int DefaultBottomPaneHeight = 200;
    private const int DefaultSidePaneWidth = 303;

        private static string LayoutFilePath => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "MainWindowDockLayout.xml");

        private readonly DockingManager _dockingManager;

        /// <summary>
        /// 持久化内容注册表，按 ContentId 索引。
        /// 初始化时填充，确保内容在关闭/重置/加载后始终可恢复。
        /// </summary>
        private readonly Dictionary<string, object> _contentRegistry = new();

        /// <summary>
        /// 面板元数据注册表，存储每个面板的标题和默认位置信息。
        /// </summary>
        private readonly Dictionary<string, PanelInfo> _panelInfoRegistry = new();

        /// <summary>
        /// 文档注册表，存储 LayoutDocument 的内容信息。
        /// </summary>
        private readonly Dictionary<string, DocumentInfo> _documentRegistry = new();

        public DockLayoutManager(DockingManager dockingManager)
        {
            _dockingManager = dockingManager;
        }

        /// <summary>
        /// 注册面板内容及元数据，以便在关闭/重置/加载操作后恢复。
        /// 初始化时为每个面板调用一次。
        /// </summary>
        /// <param name="contentId">面板唯一标识</param>
        /// <param name="content">面板内容（UI 元素）</param>
        /// <param name="title">面板标题</param>
        /// <param name="position">面板默认停靠位置</param>
        public void RegisterPanel(string contentId, object content, string title, PanelPosition position = PanelPosition.Bottom)
        {
            _contentRegistry[contentId] = content;
            _panelInfoRegistry[contentId] = new PanelInfo(title, position);
        }

        /// <summary>
        /// 注册文档内容，以便在关闭/重置/加载操作后恢复。
        /// </summary>
        /// <param name="contentId">文档唯一标识</param>
        /// <param name="content">文档内容（UI 元素）</param>
        /// <param name="title">文档标题</param>
        /// <param name="canClose">是否允许关闭</param>
        public void RegisterDocument(string contentId, object content, string title, bool canClose = true)
        {
            _contentRegistry[contentId] = content;
            _documentRegistry[contentId] = new DocumentInfo(title, canClose);
        }

        /// <summary>
        /// 保存当前 AvalonDock 布局到文件。
        /// </summary>
        public void SaveLayout()
        {
            try
            {
                var serializer = new XmlLayoutSerializer(_dockingManager);
                using var stream = new StreamWriter(LayoutFilePath);
                serializer.Serialize(stream);
                log.Info("主窗口布局已保存");
            }
            catch (Exception ex)
            {
                log.Warn("保存主窗口布局失败", ex);
            }
        }

        /// <summary>
        /// 从文件加载已保存的 AvalonDock 布局。
        /// 使用持久化内容注册表恢复面板内容。
        /// </summary>
        /// <returns>是否成功加载</returns>
        public bool LoadLayout()
        {
            if (!File.Exists(LayoutFilePath)) return false;
            try
            {
                var serializer = new XmlLayoutSerializer(_dockingManager);
                serializer.LayoutSerializationCallback += (s, args) =>
                {
                    if (args.Model.ContentId != null && _contentRegistry.TryGetValue(args.Model.ContentId, out var content))
                        args.Content = content;
                    else
                        args.Cancel = true; // 取消未注册的项（如动态编辑器标签页），防止出现空内容
                };
                using var stream = new StreamReader(LayoutFilePath);
                serializer.Deserialize(stream);

                // 使用注册表中的本地化标题刷新面板/文档标题，
                // 因为序列化的 XML 可能包含旧语言环境的标题。
                RefreshTitlesFromRegistry();

                // 更新 WorkspaceManager 的引用
                WorkspaceManager.layoutRoot = _dockingManager.Layout;
                var docPane = _dockingManager.Layout.Descendents()
                    .OfType<LayoutDocumentPane>().FirstOrDefault();
                if (docPane != null)
                {
                    WorkspaceManager.LayoutDocumentPane = docPane;
                }
                else
                {
                    // 当所有动态文档被取消后，LayoutDocumentPane 可能被移除。
                    // 创建一个新的 LayoutDocumentPane 以确保视图标签页有地方添加。
                    log.Warn("加载的布局中未找到 LayoutDocumentPane，正在创建新的文档窗格");
                    EnsureDocumentPane();
                }

                // 清除旧的视图文档缓存，以便 ShowAllViews 能重新创建
                DockViewManagerHost.ClearViewDocuments();

                log.Info("主窗口布局已加载");
                return true;
            }
            catch (Exception ex)
            {
                log.Warn("加载主窗口布局失败, 将使用默认布局", ex);
                return false;
            }
        }

        /// <summary>
        /// 重置 AvalonDock 布局到默认状态。
        /// 通过编程方式重建布局，并从持久化注册表恢复内容。
        /// </summary>
        public void ResetLayout()
        {
            try
            {
                if (File.Exists(LayoutFilePath))
                {
                    try
                    {
                        File.Delete(LayoutFilePath);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        log.Warn($"无法删除布局文件（可能在受保护目录中）: {LayoutFilePath}", ex);
                    }
                }

                var defaultLayout = new LayoutRoot();
                var rootPanel = new LayoutPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };

                // 左侧面板
                var leftPanels = _panelInfoRegistry
                    .Where(kvp => kvp.Value.Position == PanelPosition.Left)
                    .ToList();

                if (leftPanels.Count > 0)
                {
                    var leftGroup = new LayoutAnchorablePaneGroup { DockWidth = new GridLength(DefaultSidePaneWidth) };
                    var leftPane = new LayoutAnchorablePane();

                    foreach (var panel in leftPanels)
                    {
                        var anchorable = new LayoutAnchorable
                        {
                            Title = panel.Value.Title,
                            ContentId = panel.Key,
                            CanClose = true,
                            CanAutoHide = true,
                            CanFloat = true
                        };
                        if (_contentRegistry.TryGetValue(panel.Key, out var content))
                            anchorable.Content = content;
                        leftPane.Children.Add(anchorable);
                    }

                    leftGroup.Children.Add(leftPane);
                    rootPanel.Children.Add(leftGroup);
                }

                // 中央 + 底部面板 (Vertical 布局)
                var centerPanel = new LayoutPanel { Orientation = System.Windows.Controls.Orientation.Vertical };

                // 中央文档区域
                var docGroup = new LayoutDocumentPaneGroup();
                var docPane = new LayoutDocumentPane();

                // 恢复已注册的文档
                foreach (var doc in _documentRegistry)
                {
                    var layoutDoc = new LayoutDocument
                    {
                        Title = doc.Value.Title,
                        ContentId = doc.Key,
                        CanClose = doc.Value.CanClose
                    };
                    if (_contentRegistry.TryGetValue(doc.Key, out var content))
                        layoutDoc.Content = content;
                    docPane.Children.Add(layoutDoc);
                }

                docGroup.Children.Add(docPane);
                centerPanel.Children.Add(docGroup);

                // 底部面板
                var bottomPanels = _panelInfoRegistry
                    .Where(kvp => kvp.Value.Position == PanelPosition.Bottom)
                    .ToList();

                if (bottomPanels.Count > 0)
                {
                    var bottomGroup = new LayoutAnchorablePaneGroup { DockHeight = new GridLength(DefaultBottomPaneHeight) };
                    var bottomPane = new LayoutAnchorablePane();

                    foreach (var panel in bottomPanels)
                    {
                        var anchorable = new LayoutAnchorable
                        {
                            Title = panel.Value.Title,
                            ContentId = panel.Key,
                            CanClose = true,
                            CanAutoHide = true,
                            CanFloat = true
                        };
                        if (_contentRegistry.TryGetValue(panel.Key, out var content))
                            anchorable.Content = content;
                        bottomPane.Children.Add(anchorable);
                    }

                    bottomGroup.Children.Add(bottomPane);
                    centerPanel.Children.Add(bottomGroup);
                }

                rootPanel.Children.Add(centerPanel);

                // 右侧面板
                var rightPanels = _panelInfoRegistry
                    .Where(kvp => kvp.Value.Position == PanelPosition.Right)
                    .ToList();

                if (rightPanels.Count > 0)
                {
                    var rightGroup = new LayoutAnchorablePaneGroup { DockWidth = new GridLength(DefaultSidePaneWidth) };
                    var rightPane = new LayoutAnchorablePane();

                    foreach (var panel in rightPanels)
                    {
                        var anchorable = new LayoutAnchorable
                        {
                            Title = panel.Value.Title,
                            ContentId = panel.Key,
                            CanClose = true,
                            CanAutoHide = true,
                            CanFloat = true
                        };
                        if (_contentRegistry.TryGetValue(panel.Key, out var content))
                            anchorable.Content = content;
                        rightPane.Children.Add(anchorable);
                    }

                    rightGroup.Children.Add(rightPane);
                    rootPanel.Children.Add(rightGroup);
                }

                defaultLayout.RootPanel = rootPanel;
                _dockingManager.Layout = defaultLayout;

                // 更新 WorkspaceManager 的引用
                WorkspaceManager.layoutRoot = defaultLayout;
                var resetDocPane = defaultLayout.Descendents()
                    .OfType<LayoutDocumentPane>().FirstOrDefault();
                if (resetDocPane != null)
                    WorkspaceManager.LayoutDocumentPane = resetDocPane;
                else
                    log.Warn("重置后的布局中未找到 LayoutDocumentPane");

                // 清除旧的视图文档缓存并重新创建所有视图标签页
                DockViewManagerHost.ClearViewDocuments();
                UI.Views.DockViewManager.GetInstance().ShowAllViews();

                log.Info("主窗口布局已重置");
            }
            catch (Exception ex)
            {
                log.Warn("重置主窗口布局失败", ex);
            }
        }

        /// <summary>
        /// 按 ContentId 切换可停靠面板的可见性。
        /// 如果面板被隐藏/关闭，则显示；如果可见，则隐藏。
        /// </summary>
        public void TogglePanel(string contentId)
        {
            var anchorable = FindAnchorable(contentId);
            if (anchorable != null)
            {
                if (anchorable.IsHidden)
                    anchorable.Show();
                else
                    anchorable.Hide();
                return;
            }

            // 面板已关闭并从布局树中移除 — 从注册表重新添加
            if (_contentRegistry.TryGetValue(contentId, out var content))
            {
                var title = _panelInfoRegistry.TryGetValue(contentId, out var info) ? info.Title : contentId;

                var newAnchorable = new LayoutAnchorable
                {
                    ContentId = contentId,
                    Title = title,
                    Content = content,
                    CanClose = true,
                    CanAutoHide = true,
                    CanFloat = true
                };

                // 找到已有的可停靠面板或创建新的底部面板
                var existingPane = _dockingManager.Layout.Descendents()
                    .OfType<LayoutAnchorablePane>().FirstOrDefault();
                if (existingPane != null)
                {
                    existingPane.Children.Add(newAnchorable);
                }
                else if (_dockingManager.Layout.RootPanel != null)
                {
                    var pane = new LayoutAnchorablePane();
                    pane.Children.Add(newAnchorable);
                    var group = new LayoutAnchorablePaneGroup { DockHeight = new GridLength(DefaultBottomPaneHeight) };
                    group.Children.Add(pane);
                    _dockingManager.Layout.RootPanel.Children.Add(group);
                }

                newAnchorable.Show();
            }
        }

        /// <summary>
        /// 检查面板是否当前可见
        /// </summary>
        public bool IsPanelVisible(string contentId)
        {
            var anchorable = FindAnchorable(contentId);
            return anchorable != null && !anchorable.IsHidden;
        }

        /// <summary>
        /// 获取所有已注册面板的 ID 列表
        /// </summary>
        public IReadOnlyList<string> GetRegisteredPanelIds()
        {
            return _panelInfoRegistry.Keys.ToList().AsReadOnly();
        }

        /// <summary>
        /// 获取面板元数据
        /// </summary>
        public PanelInfo? GetPanelInfo(string contentId)
        {
            return _panelInfoRegistry.TryGetValue(contentId, out var info) ? info : null;
        }

        /// <summary>
        /// 使用注册表中的本地化标题刷新所有面板和文档的标题。
        /// 在反序列化布局后调用，确保面板标题使用当前语言环境。
        /// </summary>
        private void RefreshTitlesFromRegistry()
        {
            foreach (var anchorable in _dockingManager.Layout.Descendents().OfType<LayoutAnchorable>())
            {
                if (anchorable.ContentId != null && _panelInfoRegistry.TryGetValue(anchorable.ContentId, out var info))
                    anchorable.Title = info.Title;
            }

            foreach (var hidden in _dockingManager.Layout.Hidden)
            {
                if (hidden.ContentId != null && _panelInfoRegistry.TryGetValue(hidden.ContentId, out var info))
                    hidden.Title = info.Title;
            }

            foreach (var doc in _dockingManager.Layout.Descendents().OfType<LayoutDocument>())
            {
                if (doc.ContentId != null && _documentRegistry.TryGetValue(doc.ContentId, out var docInfo))
                    doc.Title = docInfo.Title;
            }
        }

        /// <summary>
        /// 在布局树中按 ContentId 查找可停靠面板（包括隐藏的）
        /// </summary>
        private LayoutAnchorable? FindAnchorable(string contentId)
        {
            // 在可见布局树中搜索
            var found = _dockingManager.Layout.Descendents()
                .OfType<LayoutAnchorable>()
                .FirstOrDefault(a => a.ContentId == contentId);
            if (found != null) return found;

            // 在隐藏的可停靠面板中搜索
            foreach (var anchorable in _dockingManager.Layout.Hidden)
            {
                if (anchorable.ContentId == contentId)
                    return anchorable;
            }

            return null;
        }

        /// <summary>
        /// 确保布局树中存在 LayoutDocumentPane。
        /// 当加载的布局中所有动态文档被取消后，AvalonDock 可能移除空的 LayoutDocumentPane。
        /// 此方法会在布局树中找到合适的位置创建一个新的 LayoutDocumentPane。
        /// </summary>
        private void EnsureDocumentPane()
        {
            var layout = _dockingManager.Layout;
            var existingPane = layout.Descendents()
                .OfType<LayoutDocumentPane>().FirstOrDefault();
            if (existingPane != null)
            {
                WorkspaceManager.LayoutDocumentPane = existingPane;
                return;
            }

            // 创建新的文档窗格
            var docPane = new LayoutDocumentPane();
            var docGroup = new LayoutDocumentPaneGroup();
            docGroup.Children.Add(docPane);

            // 添加到根面板的合适位置
            if (layout.RootPanel != null)
            {
                // 在 Vertical 子面板中查找合适位置（在面板之间）
                var verticalPanel = layout.RootPanel.Descendents()
                    .OfType<LayoutPanel>()
                    .FirstOrDefault(p => p.Orientation == System.Windows.Controls.Orientation.Vertical);

                if (verticalPanel != null)
                    verticalPanel.Children.Insert(0, docGroup);
                else
                    layout.RootPanel.Children.Insert(0, docGroup);
            }
            else
            {
                var rootPanel = new LayoutPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
                rootPanel.Children.Add(docGroup);
                layout.RootPanel = rootPanel;
            }

            WorkspaceManager.LayoutDocumentPane = docPane;
        }
    }

    /// <summary>
    /// 面板默认停靠位置
    /// </summary>
    public enum PanelPosition
    {
        Bottom,
        Left,
        Right
    }

    /// <summary>
    /// 面板元数据
    /// </summary>
    public record PanelInfo(string Title, PanelPosition Position);

    /// <summary>
    /// 文档元数据
    /// </summary>
    public record DocumentInfo(string Title, bool CanClose);
}
