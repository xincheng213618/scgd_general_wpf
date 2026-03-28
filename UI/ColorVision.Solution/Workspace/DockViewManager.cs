using AvalonDock.Layout;
using ColorVision.UI.Views;
using log4net;
using System.Windows.Controls;

namespace ColorVision.Solution.Workspace
{
    /// <summary>
    /// AvalonDock 文档宿主。
    /// 为 DockViewManager 单例提供 AvalonDock LayoutDocument 的创建/切换回调。
    /// 在 MainWindow 初始化时调用 Initialize() 注册回调。
    /// </summary>
    public static class DockViewManagerHost
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DockViewManagerHost));

        /// <summary>
        /// 动态获取当前文档窗格。
        /// LoadLayout 可能替换整个布局树，因此不能缓存旧的引用。
        /// </summary>
        private static LayoutDocumentPane DocumentPane => WorkspaceManager.LayoutDocumentPane;

        /// <summary>
        /// 控件 → LayoutDocument 的映射
        /// </summary>
        private static readonly Dictionary<Control, LayoutDocument> _viewDocuments = new();

        private static int _viewCounter;

        /// <summary>
        /// 初始化 DockViewManager 回调。在 MainWindow 初始化时调用。
        /// </summary>
        public static void Initialize()
        {
            var manager = DockViewManager.GetInstance();

            manager.ActiveViewHandler = control =>
            {
                var doc = EnsureDocument(control);
                ShowDocument(doc);
            };

            manager.ShowAllViewsHandler = () =>
            {
                foreach (var control in manager.Views)
                {
                    EnsureDocument(control);
                }
            };
        }

        /// <summary>
        /// 确保视图控件有对应的 LayoutDocument。如果没有则创建。
        /// </summary>
        private static LayoutDocument EnsureDocument(Control control)
        {
            if (_viewDocuments.TryGetValue(control, out var existing))
            {
                // 已创建，确保可见
                return existing;
            }

            return CreateDocumentForView(control);
        }

        /// <summary>
        /// 为视图控件创建 LayoutDocument 并添加到文档窗格。
        /// </summary>
        private static LayoutDocument CreateDocumentForView(Control control)
        {
            DetachFromParent(control);

            _viewCounter++;
            string title = $"View {_viewCounter}";
            var manager = DockViewManager.GetInstance();
            if (manager.ViewTitles.TryGetValue(control, out var registeredTitle) && !string.IsNullOrEmpty(registeredTitle))
                title = registeredTitle;

            string contentId = $"DockView_{_viewCounter}";

            var doc = new LayoutDocument
            {
                Title = title,
                ContentId = contentId,
                Content = control,
                CanClose = true,
                CanFloat = true
            };

            // 用户关闭文档标签时，同步清理内部映射
            doc.Closing += (s, e) =>
            {
                _viewDocuments.Remove(control);
            };

            DocumentPane.Children.Add(doc);
            _viewDocuments[control] = doc;

            log.Debug($"DockViewManagerHost: 创建视图文档 '{title}' (ContentId={contentId})");
            return doc;
        }

        /// <summary>
        /// 显示 LayoutDocument（如果已关闭，重新添加到窗格）
        /// </summary>
        private static void ShowDocument(LayoutDocument doc)
        {
            if (doc.Parent == null)
                DocumentPane.Children.Add(doc);
            doc.IsActive = true;
        }

        /// <summary>
        /// 从父容器中安全移除控件
        /// </summary>
        private static void DetachFromParent(Control control)
        {
            if (control.Parent is System.Windows.Controls.Panel panel)
                panel.Children.Remove(control);
        }
    }
}
