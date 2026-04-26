using AvalonDock;
using AvalonDock.Layout;
using AvalonDock.Layout.Serialization;
using ColorVision.UI;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace ProjectStarkSemi.Layout
{
    /// <summary>
    /// ConoscopeWindow AvalonDock layout manager.
    /// Handles layout persistence, reset, and panel visibility.
    /// </summary>
    internal class DockLayoutManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DockLayoutManager));

        private const int DefaultControlPanelWidth = 320;
        private const int DefaultRightPanelWidth = 750;
        private const int DefaultBottomPaneHeight = 400;

        /// <summary>
        /// Layout file stored in user's AppData directory.
        /// </summary>
        private static string LayoutFilePath => Path.Combine(
            Environments.DirAppData, "ConoscopeWindowDockLayout.xml");

        private readonly DockingManager _dockingManager;
        private readonly Dictionary<string, object> _contentRegistry = new();

        public DockLayoutManager(DockingManager dockingManager)
        {
            _dockingManager = dockingManager;
        }

        /// <summary>
        /// Register panel content for persistence.
        /// </summary>
        public void RegisterContent(string contentId, object content)
        {
            _contentRegistry[contentId] = content;
        }

        /// <summary>
        /// Save current layout to file.
        /// </summary>
        public void SaveLayout()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LayoutFilePath)!);
                var serializer = new XmlLayoutSerializer(_dockingManager);
                using var stream = new StreamWriter(LayoutFilePath);
                serializer.Serialize(stream);
                log.Info("ConoscopeWindow layout saved");
            }
            catch (Exception ex)
            {
                log.Warn("Failed to save ConoscopeWindow layout", ex);
            }
        }

        /// <summary>
        /// Load saved layout from file.
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
                log.Info("ConoscopeWindow layout loaded");
            }
            catch (Exception ex)
            {
                log.Warn("Failed to load ConoscopeWindow layout, using default", ex);
            }
        }

        /// <summary>
        /// Reset layout to default state.
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

                // Center + Right (vertical split)
                var centerPanel = new LayoutPanel { Orientation = System.Windows.Controls.Orientation.Vertical };

                // Center: Image View (Document)
                var docGroup = new LayoutDocumentPaneGroup();
                var docPane = new LayoutDocumentPane();
                var imageDoc = new LayoutDocument
                {
                    Title = "图像显示",
                    ContentId = "ImageView",
                    CanClose = false
                };
                if (_contentRegistry.TryGetValue("ImageView", out var imgContent))
                    imageDoc.Content = imgContent;
                docPane.Children.Add(imageDoc);
                docGroup.Children.Add(docPane);
                centerPanel.Children.Add(docGroup);

                // Right side: ChannelPanel + Plots (horizontal)
                var rightPanel = new LayoutPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };

                // Right: ChannelPanel (Anchorable)
                var channelGroup = new LayoutAnchorablePaneGroup { DockWidth = new GridLength(DefaultRightPanelWidth) };
                var channelPane = new LayoutAnchorablePane();
                var channelAnchorable = new LayoutAnchorable
                {
                    Title = "通道与导出",
                    ContentId = "ChannelPanel",
                    CanClose = true,
                    CanAutoHide = true,
                    CanFloat = true
                };
                if (_contentRegistry.TryGetValue("ChannelPanel", out var channelContent))
                    channelAnchorable.Content = channelContent;
                channelPane.Children.Add(channelAnchorable);
                channelGroup.Children.Add(channelPane);
                rightPanel.Children.Add(channelGroup);

                // Bottom right: Plots (Tabbed Anchorables)
                var plotsGroup = new LayoutAnchorablePaneGroup { DockHeight = new GridLength(DefaultBottomPaneHeight) };
                var plotsPane = new LayoutAnchorablePane();

                var azimuthAnchorable = new LayoutAnchorable
                {
                    Title = "方位角",
                    ContentId = "AzimuthPlot",
                    CanClose = true,
                    CanAutoHide = true,
                    CanFloat = true
                };
                if (_contentRegistry.TryGetValue("AzimuthPlot", out var azimuthContent))
                    azimuthAnchorable.Content = azimuthContent;
                plotsPane.Children.Add(azimuthAnchorable);

                var polarAnchorable = new LayoutAnchorable
                {
                    Title = "极角",
                    ContentId = "PolarPlot",
                    CanClose = true,
                    CanAutoHide = true,
                    CanFloat = true
                };
                if (_contentRegistry.TryGetValue("PolarPlot", out var polarContent))
                    polarAnchorable.Content = polarContent;
                plotsPane.Children.Add(polarAnchorable);

                var settingAnchorable = new LayoutAnchorable
                {
                    Title = "设置",
                    ContentId = "SettingPanel",
                    CanClose = true,
                    CanAutoHide = true,
                    CanFloat = true
                };
                if (_contentRegistry.TryGetValue("SettingPanel", out var settingContent))
                    settingAnchorable.Content = settingContent;
                plotsPane.Children.Add(settingAnchorable);

                plotsGroup.Children.Add(plotsPane);
                rightPanel.Children.Add(plotsGroup);

                centerPanel.Children.Add(rightPanel);
                mainPanel.Children.Add(centerPanel);

                defaultLayout.RootPanel = mainPanel;
                _dockingManager.Layout = defaultLayout;

                log.Info("ConoscopeWindow layout reset");
            }
            catch (Exception ex)
            {
                log.Warn("Failed to reset ConoscopeWindow layout", ex);
            }
        }

        /// <summary>
        /// Toggle panel visibility by ContentId.
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
                    "ControlPanel" => "控制面板",
                    "ChannelPanel" => "通道与导出",
                    "AzimuthPlot" => "方位角",
                    "PolarPlot" => "极角",
                    "SettingPanel" => "设置",
                    _ => contentId
                };

                // Find an existing anchorable pane to add to
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
                    var group = new LayoutAnchorablePaneGroup { DockWidth = new GridLength(DefaultControlPanelWidth) };
                    group.Children.Add(pane);
                    _dockingManager.Layout.RootPanel.Children.Add(group);
                }

                newAnchorable.Show();
            }
        }

        /// <summary>
        /// Show and activate a panel.
        /// </summary>
        public void ShowPanel(string contentId)
        {
            var anchorable = FindAnchorable(contentId);
            if (anchorable != null)
            {
                if (anchorable.IsHidden)
                    anchorable.Show();
                anchorable.IsActive = true;
                return;
            }

            // Re-create if closed
            TogglePanel(contentId);
        }

        /// <summary>
        /// Check if panel is visible.
        /// </summary>
        public bool IsPanelVisible(string contentId)
        {
            var anchorable = FindAnchorable(contentId);
            return anchorable != null && !anchorable.IsHidden;
        }

        /// <summary>
        /// Find anchorable by ContentId (including hidden).
        /// </summary>
        private LayoutAnchorable? FindAnchorable(string contentId)
        {
            var found = _dockingManager.Layout.Descendents()
                .OfType<LayoutAnchorable>()
                .FirstOrDefault(a => a.ContentId == contentId);
            if (found != null) return found;

            foreach (var anchorable in _dockingManager.Layout.Hidden)
            {
                if (anchorable.ContentId == contentId)
                    return anchorable;
            }

            return null;
        }
    }
}
