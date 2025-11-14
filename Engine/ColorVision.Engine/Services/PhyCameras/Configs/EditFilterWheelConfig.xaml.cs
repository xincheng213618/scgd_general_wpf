using ColorVision.Themes;
using System;
using System.Collections.Generic;
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
        private ObservableCollection<HoleMapViewModel> WorkingCopy { get; set; }

        /// <summary>
        /// Filter options available for selection
        /// </summary>
        public List<string> FilterOptions => FilterWheelConfig.FilterOptions;

        public EditFilterWheelConfig(FilterWheelConfig filterWheelConfig)
        {
            FilterWheelConfig = filterWheelConfig;
            // Create a working copy of the collection
            WorkingCopy = new ObservableCollection<HoleMapViewModel>();
            foreach (var item in filterWheelConfig.HoleMapping)
            {
                WorkingCopy.Add(new HoleMapViewModel(filterWheelConfig)
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

        private void TextBoxPositionsPerWheel_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // Refresh the position display for all items when positions per wheel changes
            foreach (var item in WorkingCopy)
            {
                item.OnPropertyChanged(nameof(item.PositionDisplay));
            }
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

            var newHoleMap = new HoleMapViewModel(FilterWheelConfig)
            { 
                HoleIndex = maxIndex + 1, 
                HoleName = "EMPTY" 
            };
            WorkingCopy.Add(newHoleMap);
        }

        private void Button_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (DataGridHoleMapping.SelectedItem is HoleMapViewModel selectedItem)
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

    /// <summary>
    /// ViewModel for HoleMap to include computed display properties
    /// </summary>
    public class HoleMapViewModel : HoleMap
    {
        private readonly FilterWheelConfig _config;

        public HoleMapViewModel(FilterWheelConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// Display string showing the wheel and position (e.g., "1-0", "2-5")
        /// </summary>
        public string PositionDisplay => GetPositionDisplay(_config.PositionsPerWheel);
    }
}
