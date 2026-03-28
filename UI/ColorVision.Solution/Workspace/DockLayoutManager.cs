using AvalonDock;
using AvalonDock.Layout;
using AvalonDock.Layout.Serialization;
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
                };
                using var stream = new StreamReader(LayoutFilePath);
                serializer.Deserialize(stream);

                // 更新 WorkspaceManager 的引用
                WorkspaceManager.layoutRoot = _dockingManager.Layout;
                WorkspaceManager.LayoutDocumentPane = _dockingManager.Layout.Descendents()
                    .OfType<LayoutDocumentPane>().FirstOrDefault();

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
                    File.Delete(LayoutFilePath);

                var defaultLayout = new LayoutRoot();
                var mainPanel = new LayoutPanel { Orientation = System.Windows.Controls.Orientation.Vertical };

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
                mainPanel.Children.Add(docGroup);

                // 按位置分组注册的面板
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
                    mainPanel.Children.Add(bottomGroup);
                }

                defaultLayout.RootPanel = mainPanel;
                _dockingManager.Layout = defaultLayout;

                // 更新 WorkspaceManager 的引用
                WorkspaceManager.layoutRoot = defaultLayout;
                WorkspaceManager.LayoutDocumentPane = defaultLayout.Descendents()
                    .OfType<LayoutDocumentPane>().FirstOrDefault();

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
