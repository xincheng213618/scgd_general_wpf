using AvalonDock;
using AvalonDock.Layout;
using AvalonDock.Layout.Serialization;
using log4net;
using System.IO;
using System.Linq;
using System.Windows;

namespace Spectrum
{
    /// <summary>
    /// Manages AvalonDock layout persistence, reset, and panel visibility for the Spectrum MainWindow.
    /// Maintains a persistent content registry so that panel content is never lost, even after
    /// the user closes an anchorable panel.
    /// </summary>
    internal class DockLayoutManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DockLayoutManager));

        private const int DefaultControlPanelWidth = 330;
        private const int DefaultBottomPaneHeight = 250;

        private static string LayoutFilePath => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "DockLayout.xml");

        private readonly DockingManager _dockingManager;

        /// <summary>
        /// Persistent content registry keyed by ContentId.
        /// Populated once during initialization so content can always be recovered.
        /// </summary>
        private readonly Dictionary<string, object> _contentRegistry = new();

        public DockLayoutManager(DockingManager dockingManager)
        {
            _dockingManager = dockingManager;
        }

        /// <summary>
        /// Register a panel's content so it can be recovered after close/reset/load operations.
        /// Call this once for each panel during initialization.
        /// </summary>
        public void RegisterContent(string contentId, object content)
        {
            _contentRegistry[contentId] = content;
        }

        /// <summary>
        /// Save the current AvalonDock layout to file.
        /// </summary>
        public void SaveLayout()
        {
            try
            {
                var serializer = new XmlLayoutSerializer(_dockingManager);
                using var stream = new StreamWriter(LayoutFilePath);
                serializer.Serialize(stream);
                log.Info("窗口布局已保存");
            }
            catch (Exception ex)
            {
                log.Warn("保存窗口布局失败", ex);
            }
        }

        /// <summary>
        /// Load a saved AvalonDock layout from file.
        /// Uses the persistent content registry to restore panel content.
        /// </summary>
        public void LoadLayout()
        {
            if (!File.Exists(LayoutFilePath)) return;
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
                log.Info("窗口布局已加载");
            }
            catch (Exception ex)
            {
                log.Warn("加载窗口布局失败, 将使用默认布局", ex);
            }
        }

        /// <summary>
        /// Reset the AvalonDock layout to its default state.
        /// Rebuilds the layout programmatically and restores content from the persistent registry.
        /// </summary>
        public void ResetLayout()
        {
            try
            {
                if (File.Exists(LayoutFilePath))
                    File.Delete(LayoutFilePath);

                var defaultLayout = new LayoutRoot();
                var mainPanel = new LayoutPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };

                // Left: Control Panel
                var leftGroup = new LayoutAnchorablePaneGroup { DockWidth = new GridLength(DefaultControlPanelWidth) };
                var leftPane = new LayoutAnchorablePane();
                var controlPanel = new LayoutAnchorable
                {
                    Title = "控制面板",
                    ContentId = "ControlPanel",
                    CanClose = false,
                    CanAutoHide = true,
                    CanFloat = true
                };
                if (_contentRegistry.TryGetValue("ControlPanel", out var cpContent))
                    controlPanel.Content = cpContent;
                leftPane.Children.Add(controlPanel);
                leftGroup.Children.Add(leftPane);
                mainPanel.Children.Add(leftGroup);

                // Center + Bottom
                var centerPanel = new LayoutPanel { Orientation = System.Windows.Controls.Orientation.Vertical };

                // Center: Spectrum Chart document
                var docGroup = new LayoutDocumentPaneGroup();
                var docPane = new LayoutDocumentPane();
                var chartDoc = new LayoutDocument
                {
                    Title = "光谱图表",
                    ContentId = "SpectrumChart",
                    CanClose = false
                };
                if (_contentRegistry.TryGetValue("SpectrumChart", out var chartContent))
                    chartDoc.Content = chartContent;
                docPane.Children.Add(chartDoc);
                docGroup.Children.Add(docPane);
                centerPanel.Children.Add(docGroup);

                // Bottom: Log + CIE Diagram
                var bottomGroup = new LayoutAnchorablePaneGroup { DockHeight = new GridLength(DefaultBottomPaneHeight) };
                var bottomPane = new LayoutAnchorablePane();

                var logAnchorable = new LayoutAnchorable
                {
                    Title = "日志",
                    ContentId = "LogPanel",
                    CanClose = true,
                    CanAutoHide = true,
                    CanFloat = true
                };
                if (_contentRegistry.TryGetValue("LogPanel", out var logContent))
                    logAnchorable.Content = logContent;
                bottomPane.Children.Add(logAnchorable);

                var cieAnchorable = new LayoutAnchorable
                {
                    Title = "CIE色度图",
                    ContentId = "CIEDiagram",
                    CanClose = true,
                    CanAutoHide = true,
                    CanFloat = true
                };
                if (_contentRegistry.TryGetValue("CIEDiagram", out var cieContent))
                    cieAnchorable.Content = cieContent;
                bottomPane.Children.Add(cieAnchorable);

                bottomGroup.Children.Add(bottomPane);
                centerPanel.Children.Add(bottomGroup);

                mainPanel.Children.Add(centerPanel);
                defaultLayout.RootPanel = mainPanel;

                _dockingManager.Layout = defaultLayout;
                log.Info("窗口布局已重置");
            }
            catch (Exception ex)
            {
                log.Warn("重置窗口布局失败", ex);
            }
        }

        /// <summary>
        /// Toggle an anchorable panel's visibility by ContentId.
        /// If the panel is hidden/closed, it will be shown; if visible, it will be hidden.
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

            // Panel was closed and removed from layout tree - re-add it
            if (_contentRegistry.TryGetValue(contentId, out var content))
            {
                var newAnchorable = new LayoutAnchorable
                {
                    ContentId = contentId,
                    Content = content,
                    CanClose = true,
                    CanAutoHide = true,
                    CanFloat = true
                };

                // Set title based on known ContentIds
                newAnchorable.Title = contentId switch
                {
                    "LogPanel" => "日志",
                    "CIEDiagram" => "CIE色度图",
                    _ => contentId
                };

                // Find an existing anchorable pane to add to, or the first document pane
                var existingPane = _dockingManager.Layout.Descendents()
                    .OfType<LayoutAnchorablePane>().FirstOrDefault();
                if (existingPane != null)
                {
                    existingPane.Children.Add(newAnchorable);
                }
                else if (_dockingManager.Layout.RootPanel != null)
                {
                    // Create a new bottom pane and add to root panel
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
        /// Find an anchorable in the layout tree by ContentId,
        /// including hidden anchorables.
        /// </summary>
        private LayoutAnchorable? FindAnchorable(string contentId)
        {
            // Search in visible layout tree
            var found = _dockingManager.Layout.Descendents()
                .OfType<LayoutAnchorable>()
                .FirstOrDefault(a => a.ContentId == contentId);
            if (found != null) return found;

            // Search in hidden anchorables
            foreach (var anchorable in _dockingManager.Layout.Hidden)
            {
                if (anchorable.ContentId == contentId)
                    return anchorable;
            }

            return null;
        }
    }
}
