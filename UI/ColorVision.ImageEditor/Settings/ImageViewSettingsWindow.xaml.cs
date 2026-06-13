#pragma warning disable CA1859
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Ruler;
using ColorVision.ImageEditor.EditorTools.Filters;
using ColorVision.ImageEditor.EditorTools.PseudoColor;
using ColorVision.ImageEditor.Tif;
using ColorVision.UI;
using System;
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
            SettingsTabControl.Items.Clear();

            StackPanel display = AddTab(EditorResources.Settings_GroupDisplay);
            AddProperty(display, _imageView.Config, nameof(ImageViewConfig.IsLayoutUpdated));
            AddProperty(display, _imageView.Config, nameof(ImageViewConfig.IsShowText));
            AddProperty(display, _imageView.Config, nameof(ImageViewConfig.IsShowMsg));
            AddProperty(display, _imageView.Config, nameof(ImageViewConfig.DrawingTextFontSize));

            StackPanel context = AddTab(EditorResources.Settings_GroupContext);
            AddView(context, new ImageViewContextSettingsView(_imageView));

            StackPanel defaults = AddTab(EditorResources.Settings_GroupDefaults);
            AddObject(defaults, EditorResources.Settings_DefaultImageScaling, DefaultBitmapScalingConfig.Current);
            AddObject(defaults, EditorResources.Settings_DefaultDisplayParams, DefaultImageViewDisplayConfig.Current);
            AddObject(defaults, EditorResources.Settings_DefaultTextStyle, DefaultTextStyleConfig.Current);
            AddObject(defaults, EditorResources.Settings_PhysicalSizeDefaults, DefalutTextAttribute.Defalut);
            AddObject(defaults, EditorResources.Settings_DefaultRealtimeCameraParams, DefaultRealtimeCameraConfig.Current);

            StackPanel workspace = AddTab(EditorResources.Settings_GroupWorkspace);
            AddView(workspace, new ImageViewWorkspaceSettingsView(_imageView));

            StackPanel loader = AddTab(EditorResources.Settings_GroupLoader);
            AddObject(loader, EditorResources.Settings_TifOpener, TifOpenConfig.Current);

            if (_imageView.IEditorToolFactory.GetIEditorTool<DisplayShaderFilterEditorTool>() is DisplayShaderFilterEditorTool shaderFilter)
            {
                StackPanel shader = AddTab("Shader Filter");
                AddObject(shader, "Current shader filter", shaderFilter.State);
            }

            if (_imageView.IEditorToolFactory.GetIEditorTool<PseudoColorEditorTool>() is PseudoColorEditorTool pseudoColor)
            {
                StackPanel pseudo = AddTab(EditorResources.PseudoColor_Group);
                AddObject(pseudo, EditorResources.PseudoColor_CurrentPseudoColor, pseudoColor.State);
                AddObject(pseudo, EditorResources.PseudoColor_DefaultPseudoColor, PseudoColorDefaultConfig.Current);
            }

            SelectInitialGroup();
        }

        private StackPanel AddTab(string header)
        {
            StackPanel stackPanel = new() { Margin = new Thickness(10) };
            SettingsTabControl.Items.Add(new TabItem
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
            StackPanel stackPanel = new();
            stackPanel.Children.Add(new TextBlock { Margin = new Thickness(0, 0, 0, 6), FontWeight = FontWeights.SemiBold, Text = title });
            stackPanel.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(source));
            AddSection(panel, stackPanel);
        }

        private static void AddView(Panel panel, FrameworkElement view)
        {
            AddSection(panel, view);
        }

        private static void AddSection(Panel panel, UIElement content)
        {
            Border border = new()
            {
                Child = content,
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(10),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
            };
            border.SetResourceReference(Border.BackgroundProperty, "GlobalBorderBrush");
            border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            panel.Children.Add(border);
        }

        private void SelectInitialGroup()
        {
            if (SettingsTabControl.Items.Count == 0) return;
            if (!string.IsNullOrWhiteSpace(_initialGroup))
            {
                foreach (TabItem tabItem in SettingsTabControl.Items.OfType<TabItem>())
                {
                    if (string.Equals(tabItem.Header?.ToString(), _initialGroup, StringComparison.OrdinalIgnoreCase))
                    {
                        SettingsTabControl.SelectedItem = tabItem;
                        return;
                    }
                }
            }

            SettingsTabControl.SelectedIndex = 0;
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
    }
}
