using ColorVision.UI;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Properties = ColorVision.Engine.Properties;

namespace ColorVision.Engine.Batch
{
    public class PreTypeNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IPreProcess process)
            {
                var metadata = PreProcessMetadata.FromProcess(process);
                return parameter?.ToString() switch
                {
                    "Description" => metadata.Description,
                    "Category" => metadata.Category,
                    "Enabled" => process.GetConfig() is PreProcessConfigBase { IsEnabled: true } ? Properties.Resources.Flow_PreProcess_EnabledStatus : Properties.Resources.Flow_PreProcess_DisabledStatus,
                    "Templates" => GetTemplateText(process),
                    "TypeName" => metadata.TypeName,
                    _ => metadata.DisplayName,
                };
            }
            if (value is ListViewItem item && parameter?.ToString() == "Index")
            {
                var listView = FindParent<ListView>(item);
                if (listView != null)
                {
                    int index = listView.Items.IndexOf(item.Content);
                    return (index + 1).ToString();
                }
            }
            return value?.GetType().Name ?? string.Empty;
        }

        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = System.Windows.Media.VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindParent<T>(parentObject);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }

        private static string GetTemplateText(IPreProcess process)
        {
            if (process.GetConfig() is not PreProcessConfigBase config)
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(config.TemplateNames) ? Properties.Resources.Flow_PreProcess_AllTemplates : config.TemplateNames;
        }
    }

    public class PreProcessTooltipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IPreProcess process)
            {
                var metadata = PreProcessMetadata.FromProcess(process);
                return metadata.GetTooltipText();
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class PreBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    /// <summary>
    /// PreProcessManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class PreProcessManagerWindow : Window
    {
        public PreProcessManagerWindow()
        {
            InitializeComponent();
            Closing += Window_Closing;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            PreProcessManager.GetInstance().SavePersisted();
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshPropertyPanel();
        }

        private void RefreshPropertyPanel()
        {
            PropertyPanel.Children.Clear();

            var manager = DataContext as PreProcessManager;
            var selectedProcess = manager?.SelectedProcess;

            if (selectedProcess == null)
            {
                // Show placeholder text
                PropertyPanel.Children.Add(new TextBlock 
                { 
                    Text = Properties.Resources.Flow_PreProcess_SelectProcessorToViewConfig,
                    Foreground = System.Windows.Media.Brushes.Gray,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 20, 0, 0)
                });
                return;
            }

            // Add processor info section
            AddProcessorInfoSection(selectedProcess);

            // Add pre-processor config if available
            var config = selectedProcess.GetConfig();
            if (config != null)
            {
                AddConfigSection(config);
            }
        }

        private void AddProcessorInfoSection(IPreProcess process)
        {
            var metadata = PreProcessMetadata.FromProcess(process);
            
            var border = new Border
            {
                BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var stack = new StackPanel();
            border.Child = stack;

            // Header
            stack.Children.Add(new TextBlock 
            { 
                Text = Properties.Resources.BasicInformation,
                FontWeight = FontWeights.Bold, 
                Margin = new Thickness(0, 0, 0, 8) 
            });

            // Process Type
            AddLabeledText(stack, Properties.Resources.HandleClass + ":", metadata.DisplayName);
            AddLabeledText(stack, Properties.Resources.Type + ":", metadata.TypeName);

            // Category
            if (!string.IsNullOrEmpty(metadata.Category))
            {
                AddLabeledText(stack, Properties.Resources.Flow_PreProcess_Category + ":", metadata.Category);
            }

            // Description
            if (!string.IsNullOrEmpty(metadata.Description))
            {
                AddLabeledText(stack, Properties.Resources.Description + ":", metadata.Description);
            }

            PropertyPanel.Children.Add(border);
        }

        private void AddConfigSection(object config)
        {
            var border = new Border
            {
                BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var stack = new StackPanel();
            border.Child = stack;

            // Header
            stack.Children.Add(new TextBlock 
            { 
                Text = Properties.Resources.Flow_PreProcess_ProcessorConfig,
                FontWeight = FontWeights.Bold, 
                Margin = new Thickness(0, 0, 0, 8) 
            });

            // Generate property editor controls
            var configPanel = PropertyEditorHelper.GenPropertyEditorControl(config);

            stack.Children.Add(configPanel);

            PropertyPanel.Children.Add(border);
        }

        private static void AddLabeledText(StackPanel parent, string label, string value)
        {
            var dock = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            dock.Children.Add(new TextBlock 
            { 
                Text = label, 
                Width = 70, 
                VerticalAlignment = VerticalAlignment.Center 
            });
            dock.Children.Add(new TextBlock 
            { 
                Text = value ?? "", 
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            });
            parent.Children.Add(dock);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
