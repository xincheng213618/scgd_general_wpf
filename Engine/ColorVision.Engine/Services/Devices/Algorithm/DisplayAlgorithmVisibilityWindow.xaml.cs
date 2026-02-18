using ColorVision.Common.MVVM;
using ColorVision.Themes;
using ColorVision.UI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Algorithm
{
    public class AlgorithmVisibilityItem : ViewModelBase
    {
        private readonly DisplayAlgorithmVisibilityConfig _config;

        public string Name { get; set; }
        public string Group { get; set; }
        public int DefaultOrder { get; set; }

        public bool IsVisible
        {
            get => _config.GetAlgorithmVisibility(Name);
            set
            {
                _config.SetAlgorithmVisibility(Name, value);
                OnPropertyChanged();
            }
        }

        public int Order
        {
            get => _config.GetOrderOverride(Name, DefaultOrder);
            set
            {
                _config.SetOrderOverride(Name, value);
                OnPropertyChanged();
            }
        }

        public string DisplayName
        {
            get => _config.GetNameOverride(Name);
            set
            {
                _config.SetNameOverride(Name, value);
                OnPropertyChanged();
            }
        }

        public AlgorithmVisibilityItem(string name, string group, int defaultOrder, DisplayAlgorithmVisibilityConfig config)
        {
            Name = name;
            Group = group;
            DefaultOrder = defaultOrder;
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
                        AlgorithmItems.Add(new AlgorithmVisibilityItem(attr.Name, attr.Group, attr.Order, config));
                    }
                }
            }

            var sorted = new ObservableCollection<AlgorithmVisibilityItem>(
                AlgorithmItems.OrderBy(a => a.Group).ThenBy(a => a.Order).ThenBy(a => a.Name));
            AlgorithmItems = sorted;

            AlgorithmListView.ItemsSource = AlgorithmItems;

            // Build group filter
            var groups = AlgorithmItems.Select(a => a.Group).Distinct().OrderBy(g => g).ToList();
            groups.Insert(0, "All");
            CB_GroupFilter.ItemsSource = groups;
            CB_GroupFilter.SelectedIndex = 0;
        }

        private void CB_GroupFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CB_GroupFilter.SelectedItem is string selectedGroup && AlgorithmItems != null)
            {
                if (selectedGroup == "All")
                    AlgorithmListView.ItemsSource = AlgorithmItems;
                else
                    AlgorithmListView.ItemsSource = new ObservableCollection<AlgorithmVisibilityItem>(
                        AlgorithmItems.Where(a => a.Group == selectedGroup));
            }
        }

        private void ShowGroup_Click(object sender, RoutedEventArgs e)
        {
            if (CB_GroupFilter.SelectedItem is string selectedGroup)
            {
                var items = selectedGroup == "All" ? AlgorithmItems : AlgorithmItems.Where(a => a.Group == selectedGroup);
                foreach (var item in items)
                    item.IsVisible = true;
            }
        }

        private void HideGroup_Click(object sender, RoutedEventArgs e)
        {
            if (CB_GroupFilter.SelectedItem is string selectedGroup)
            {
                var items = selectedGroup == "All" ? AlgorithmItems : AlgorithmItems.Where(a => a.Group == selectedGroup);
                foreach (var item in items)
                    item.IsVisible = false;
            }
        }

        private void ShowAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in AlgorithmItems)
                item.IsVisible = true;
        }

        private void HideAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in AlgorithmItems)
                item.IsVisible = false;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
