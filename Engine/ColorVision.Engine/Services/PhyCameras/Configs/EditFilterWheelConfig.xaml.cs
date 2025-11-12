using ColorVision.Themes;
using System;
using System.Collections.ObjectModel;
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
            // Create a working copy of the collection
            WorkingCopy = new ObservableCollection<HoleMap>();
            foreach (var item in filterWheelConfig.HoleMapping)
            {
                WorkingCopy.Add(new HoleMap 
                { 
                    HoleIndex = item.HoleIndex, 
                    HoleName = item.HoleName 
                });
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
            // Find the next available hole index
            int maxIndex = -1;
            foreach (var item in WorkingCopy)
            {
                if (item.HoleIndex > maxIndex)
                    maxIndex = item.HoleIndex;
            }

            var newHoleMap = new HoleMap 
            { 
                HoleIndex = maxIndex + 1, 
                HoleName = $"Hole{maxIndex + 1}" 
            };
            WorkingCopy.Add(newHoleMap);
        }

        private void Button_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (DataGridHoleMapping.SelectedItem is HoleMap selectedItem)
            {
                WorkingCopy.Remove(selectedItem);
            }
            else
            {
                MessageBox.Show("Please select an item to delete.", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Button_Save_Click(object sender, RoutedEventArgs e)
        {
            // Update the original collection
            FilterWheelConfig.HoleMapping.Clear();
            foreach (var item in WorkingCopy)
            {
                FilterWheelConfig.HoleMapping.Add(new HoleMap 
                { 
                    HoleIndex = item.HoleIndex, 
                    HoleName = item.HoleName 
                });
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
