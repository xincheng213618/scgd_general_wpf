#pragma warning disable CA1859
using ColorVision.ImageEditor.Properties;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.ImageEditor.Settings
{
    public partial class ImageViewSettingsWindow : Window
    {
        private readonly ImageView _imageView;
        private readonly string? _initialGroup;
        private readonly List<IImageViewSettingProvider> _providers;

        public ImageViewSettingsWindow(ImageView imageView, string? initialGroup = null)
        {
            _imageView = imageView ?? throw new ArgumentNullException(nameof(imageView));
            _initialGroup = initialGroup;
            _providers = imageView.GetImageViewSettingProviders().ToList();
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            LoadSettings();
        }

        private void LoadSettings()
        {
            SettingsTabControl.Items.Clear();

            Dictionary<string, StackPanel> groupPanels = new(StringComparer.OrdinalIgnoreCase);

            IEnumerable<ImageViewSettingMetadata> settings = _providers.SelectMany(provider => provider
                .GetImageViewSettings(_imageView)
                .OrderBy(setting => setting.Order));

            foreach (ImageViewSettingMetadata setting in settings)
            {
                if (string.IsNullOrWhiteSpace(setting.Group))
                {
                    continue;
                }

                StackPanel targetPanel = GetOrCreateGroupPanel(setting.Group, groupPanels);
                AddSettingItem(setting, targetPanel);
            }

            SelectInitialGroup();
        }

        private StackPanel GetOrCreateGroupPanel(string group, IDictionary<string, StackPanel> groupPanels)
        {
            if (groupPanels.TryGetValue(group, out StackPanel? existingPanel))
            {
                return existingPanel;
            }

            StackPanel stackPanel = new() { Margin = new Thickness(10) };
            ScrollViewer scrollViewer = new()
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = stackPanel,
            };

            TabItem tabItem = new()
            {
                Header = group,
                Content = scrollViewer,
            };

            SettingsTabControl.Items.Add(tabItem);
            groupPanels[group] = stackPanel;
            return stackPanel;
        }

        private static void AddSettingItem(ImageViewSettingMetadata setting, Panel targetPanel)
        {
            switch (setting.Type)
            {
                case ImageViewSettingType.Property:
                    AddPropertyItem(setting, targetPanel);
                    break;
                case ImageViewSettingType.Class:
                    AddClassItem(setting, targetPanel);
                    break;
                case ImageViewSettingType.View:
                    AddViewItem(setting, targetPanel);
                    break;
            }
        }

        private static void AddPropertyItem(ImageViewSettingMetadata setting, Panel targetPanel)
        {
            if (setting.Source == null || string.IsNullOrWhiteSpace(setting.BindingName))
            {
                return;
            }

            DockPanel dockPanel = PropertyEditorHelper.GenProperties(setting.Source, setting.BindingName);
            dockPanel.Margin = new Thickness(0, 0, 0, 6);
            targetPanel.Children.Add(dockPanel);
        }

        private static void AddClassItem(ImageViewSettingMetadata setting, Panel targetPanel)
        {
            if (setting.Source == null)
            {
                return;
            }

            Border sectionBorder = CreateSectionContainer();
            StackPanel stackPanel = new();

            if (!string.IsNullOrWhiteSpace(setting.Name))
            {
                stackPanel.Children.Add(new TextBlock
                {
                    Margin = new Thickness(0, 0, 0, 6),
                    FontWeight = FontWeights.SemiBold,
                    Text = setting.Name,
                });
            }

            stackPanel.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(setting.Source));
            sectionBorder.Child = stackPanel;
            targetPanel.Children.Add(sectionBorder);
        }

        private static void AddViewItem(ImageViewSettingMetadata setting, Panel targetPanel)
        {
            FrameworkElement? view = setting.ViewFactory?.Invoke();
            if (view == null)
            {
                if (setting.ViewType == null)
                {
                    return;
                }

                if (Activator.CreateInstance(setting.ViewType) is not FrameworkElement createdView)
                {
                    return;
                }

                view = createdView;
            }

            Border sectionBorder = CreateSectionContainer();
            StackPanel stackPanel = new();

            if (!string.IsNullOrWhiteSpace(setting.Name))
            {
                stackPanel.Children.Add(new TextBlock
                {
                    Margin = new Thickness(0, 0, 0, 6),
                    FontWeight = FontWeights.SemiBold,
                    Text = setting.Name,
                });
            }

            stackPanel.Children.Add(view);
            sectionBorder.Child = stackPanel;
            targetPanel.Children.Add(sectionBorder);
        }

        private static Border CreateSectionContainer()
        {
            Border sectionBorder = new()
            {
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(10),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
            };
            sectionBorder.SetResourceReference(Border.BackgroundProperty, "GlobalBorderBrush");
            sectionBorder.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            return sectionBorder;
        }

        private void SelectInitialGroup()
        {
            if (SettingsTabControl.Items.Count == 0)
            {
                return;
            }

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
            foreach (IImageViewSettingPersistence provider in _providers.OfType<IImageViewSettingPersistence>())
            {
                provider.SaveImageViewSettings(_imageView);
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
    }
}
