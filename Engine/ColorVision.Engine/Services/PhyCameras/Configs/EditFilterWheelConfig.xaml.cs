#pragma warning disable CA1863
using ColorVision.Themes;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace ColorVision.Engine.Services.PhyCameras.Configs
{
    /// <summary>
    /// EditFilterWheelConfig.xaml 的交互逻辑
    /// </summary>
    public partial class EditFilterWheelConfig : Window
    {
        public FilterWheelConfig FilterWheelConfig { get; set; }
        private ObservableCollection<HoleMap> WorkingCopy { get; set; }

        public EditFilterWheelConfig(FilterWheelConfig filterWheelConfig)
        {
            FilterWheelConfig = filterWheelConfig;
            WorkingCopy = new ObservableCollection<HoleMap>();
            foreach (var item in filterWheelConfig.HoleMapping)
            {
                WorkingCopy.Add(item.Clone());
            }

            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            DataGridHoleMapping.ItemsSource = WorkingCopy;
        }

        private void Button_Add_Click(object sender, RoutedEventArgs e)
        {
            int nextIndex = WorkingCopy.Count == 0 ? 1 : WorkingCopy.Max(item => item.HoleIndex) + 1;

            var newHoleMap = new HoleMap
            {
                HoleIndex = nextIndex,
                HoleName = string.Format(Properties.Resources.FilterWheelHoleDefaultNameFormat, nextIndex)
            };
            WorkingCopy.Add(newHoleMap);
            DataGridHoleMapping.SelectedItem = newHoleMap;
            DataGridHoleMapping.ScrollIntoView(newHoleMap);
        }

        private void Button_DeleteRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: HoleMap holeMap })
            {
                WorkingCopy.Remove(holeMap);
            }
        }

        private void Button_Save_Click(object sender, RoutedEventArgs e)
        {
            FilterWheelConfig.HoleMapping.Clear();
            foreach (var item in WorkingCopy)
            {
                FilterWheelConfig.HoleMapping.Add(item.Clone());
            }

            DialogResult = true;
            Close();
        }

        private void Button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
