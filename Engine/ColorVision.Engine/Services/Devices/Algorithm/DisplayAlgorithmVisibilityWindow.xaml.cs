using ColorVision.Common.MVVM;
using ColorVision.Themes;
using ColorVision.UI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Algorithm
{
    public class AlgorithmVisibilityItem : ViewModelBase
    {
        private readonly DisplayAlgorithmVisibilityConfig _config;

        public string Name { get; set; }
        public string Group { get; set; }

        public bool IsVisible
        {
            get => _config.GetAlgorithmVisibility(Name);
            set
            {
                _config.SetAlgorithmVisibility(Name, value);
                OnPropertyChanged();
            }
        }

        public AlgorithmVisibilityItem(string name, string group, DisplayAlgorithmVisibilityConfig config)
        {
            Name = name;
            Group = group;
            _config = config;
        }
    }

    /// <summary>
    /// DisplayAlgorithmVisibilityWindow.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayAlgorithmVisibilityWindow : Window
    {
        public ObservableCollection<AlgorithmVisibilityItem> AlgorithmItems { get; set; }

        public DisplayAlgorithmVisibilityWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.ApplyCaption();

            var config = DisplayAlgorithmVisibilityConfig.Instance;
            AlgorithmItems = new ObservableCollection<AlgorithmVisibilityItem>();

            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(t => typeof(IDisplayAlgorithm).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    var attr = type.GetCustomAttribute<DisplayAlgorithmAttribute>();
                    if (attr != null)
                    {
                        AlgorithmItems.Add(new AlgorithmVisibilityItem(attr.Name, attr.Group, config));
                    }
                }
            }

            var sorted = new ObservableCollection<AlgorithmVisibilityItem>(
                AlgorithmItems.OrderBy(a => a.Group).ThenBy(a => a.Name));
            AlgorithmItems = sorted;

            AlgorithmListView.ItemsSource = AlgorithmItems;
        }

        private void ShowAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in AlgorithmItems)
            {
                item.IsVisible = true;
            }
        }

        private void HideAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in AlgorithmItems)
            {
                item.IsVisible = false;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
