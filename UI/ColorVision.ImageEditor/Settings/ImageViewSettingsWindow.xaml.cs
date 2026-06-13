#pragma warning disable CA1859
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Ruler;
using ColorVision.ImageEditor.EditorTools.Filters;
using ColorVision.ImageEditor.EditorTools.PseudoColor;
using ColorVision.ImageEditor.Tif;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using EditorResources = ColorVision.ImageEditor.Properties.Resources;

namespace ColorVision.ImageEditor.Settings
{
    public partial class ImageViewSettingsWindow : Window
    {
        private readonly ImageView _imageView;
        private readonly string? _initialGroup;
        private readonly List<SettingsPage> _pages = new();

        public ImageViewSettingsWindow(ImageView imageView, string? initialGroup = null)
        {
            _imageView = imageView ?? throw new ArgumentNullException(nameof(imageView));
            _initialGroup = initialGroup;
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            LoadSettings();
        }

        private void LoadSettings()
        {
            _pages.Clear();

            StackPanel display = AddPage(EditorResources.Settings_GroupDisplay);
            AddProperty(display, _imageView.Config, nameof(ImageViewConfig.IsLayoutUpdated));
            AddProperty(display, _imageView.Config, nameof(ImageViewConfig.IsShowText));
            AddProperty(display, _imageView.Config, nameof(ImageViewConfig.IsShowMsg));
            AddProperty(display, _imageView.Config, nameof(ImageViewConfig.DrawingTextFontSize));

            StackPanel context = AddPage(EditorResources.Settings_GroupContext);
            AddView(context, new ImageViewContextSettingsView(_imageView));

            StackPanel defaults = AddPage(EditorResources.Settings_GroupDefaults);
            AddObject(defaults, EditorResources.Settings_DefaultImageScaling, DefaultBitmapScalingConfig.Current);
            AddObject(defaults, EditorResources.Settings_DefaultDisplayParams, DefaultImageViewDisplayConfig.Current);
            AddObject(defaults, EditorResources.Settings_DefaultTextStyle, DefaultTextStyleConfig.Current);
            AddObject(defaults, EditorResources.Settings_PhysicalSizeDefaults, DefalutTextAttribute.Defalut);
            AddObject(defaults, EditorResources.Settings_DefaultRealtimeCameraParams, DefaultRealtimeCameraConfig.Current);

            StackPanel workspace = AddPage(EditorResources.Settings_GroupWorkspace);
            AddView(workspace, new ImageViewWorkspaceSettingsView(_imageView));

            StackPanel loader = AddPage(EditorResources.Settings_GroupLoader);
            AddObject(loader, EditorResources.Settings_TifOpener, TifOpenConfig.Current);

            if (_imageView.IEditorToolFactory.GetIEditorTool<DisplayShaderFilterEditorTool>() is DisplayShaderFilterEditorTool shaderFilter)
            {
                StackPanel shader = AddPage("Shader Filter");
                AddObject(shader, "当前值", shaderFilter.State);
            }

            if (_imageView.IEditorToolFactory.GetIEditorTool<PseudoColorEditorTool>() is PseudoColorEditorTool pseudoColor)
            {
                StackPanel pseudo = AddPage(EditorResources.PseudoColor_Group);
                AddObjectPair(pseudo, EditorResources.PseudoColor_CurrentPseudoColor, pseudoColor.State, EditorResources.PseudoColor_DefaultPseudoColor, PseudoColorDefaultConfig.Current);
            }

            foreach (IGrouping<string, ImageViewSettingsEntry> group in _imageView.GetRegisteredSettings().GroupBy(entry => entry.Group))
            {
                StackPanel custom = AddPage(group.Key);
                ImageViewSettingsEntry[] entries = group.ToArray();
                if (entries.Length == 2)
                {
                    AddObjectPair(custom, entries[0].Title, entries[0].Source, entries[1].Title, entries[1].Source);
                }
                else
                {
                    foreach (ImageViewSettingsEntry entry in entries)
                    {
                        AddObject(custom, entry.Title, entry.Source);
                    }
                }
            }

            SettingsList.ItemsSource = _pages;
            SelectInitialGroup();
        }

        private StackPanel AddPage(string header)
        {
            StackPanel stackPanel = new() { Margin = new Thickness(10) };
            _pages.Add(new SettingsPage
            {
                Header = header,
                Content = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = stackPanel,
                },
            });
            return stackPanel;
        }

        private static void AddProperty(Panel panel, object source, string propertyName)
        {
            DockPanel dockPanel = PropertyEditorHelper.GenProperties(source, propertyName);
            dockPanel.Margin = new Thickness(0, 0, 0, 6);
            panel.Children.Add(dockPanel);
        }

        private static void AddObject(Panel panel, string title, object source)
        {
            FrameworkElement editor = CreateEditor(source);
            StackPanel stackPanel = new();
            stackPanel.Children.Add(new TextBlock { Margin = new Thickness(0, 0, 0, 6), FontWeight = FontWeights.SemiBold, Text = title });
            stackPanel.Children.Add(editor);
            AddSection(panel, stackPanel);
        }

        private static void AddObjectPair(Panel panel, string leftTitle, object leftSource, string rightTitle, object rightSource)
        {
            Grid grid = new() { Margin = new Thickness(0, 0, 0, 14) };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());

            StackPanel left = CreateObjectPanel(leftTitle, leftSource);
            StackPanel right = CreateObjectPanel(rightTitle, rightSource);
            Grid.SetColumn(right, 2);
            grid.Children.Add(left);
            grid.Children.Add(right);
            panel.Children.Add(grid);
        }

        private static StackPanel CreateObjectPanel(string title, object source)
        {
            StackPanel stackPanel = new();
            stackPanel.Children.Add(new TextBlock { Margin = new Thickness(0, 0, 0, 6), FontWeight = FontWeights.SemiBold, Text = title });
            stackPanel.Children.Add(CreateEditor(source));
            return stackPanel;
        }

        private static StackPanel CreateEditor(object source)
        {
            return PropertyEditorHelper.GenPropertyEditorControl(source, showCategoryHeader: false);
        }

        private static void AddView(Panel panel, FrameworkElement view)
        {
            AddSection(panel, view);
        }

        private static void AddSection(Panel panel, UIElement content)
        {
            if (content is FrameworkElement element)
            {
                element.Margin = new Thickness(0, 0, 0, 14);
            }
            panel.Children.Add(content);
        }

        private void SelectInitialGroup()
        {
            if (_pages.Count == 0) return;
            if (!string.IsNullOrWhiteSpace(_initialGroup))
            {
                SettingsPage? page = _pages.FirstOrDefault(item => string.Equals(item.Header, _initialGroup, StringComparison.OrdinalIgnoreCase));
                if (page != null)
                {
                    SettingsList.SelectedItem = page;
                    return;
                }
            }

            SettingsList.SelectedIndex = 0;
        }

        private void SettingsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SettingsContent.Content = SettingsList.SelectedItem is SettingsPage page ? page.Content : null;
        }

        private void SaveSettings()
        {
            DefaultBitmapScalingConfig.SaveCurrent();
            DefaultImageViewDisplayConfig.SaveCurrent();
            DefaultTextStyleConfig.SaveCurrent();
            DefaultRealtimeCameraConfig.SaveCurrent();
            ImageCalibrationService.SaveCurrent(_imageView.Config);
            TifOpenConfig.SaveCurrent();
            PseudoColorDefaultConfig.SaveCurrent();
            _imageView.IEditorToolFactory.GetIEditorTool<DisplayShaderFilterEditorTool>()?.Save();
            foreach (ImageViewSettingsEntry entry in _imageView.GetRegisteredSettings())
            {
                entry.Save?.Invoke();
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            SaveSettings();
            base.OnClosed(e);
        }

        private sealed class SettingsPage
        {
            public string Header { get; init; } = string.Empty;

            public FrameworkElement Content { get; init; } = new Grid();
        }
    }
}
