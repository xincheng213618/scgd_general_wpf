using ColorVision.Engine.Services.Core;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.PhyCameras.Group
{
    public partial class ResourceManagerWindow : Window
    {
        public BaseResourceObject DeviceService { get;set;}

        public ResourceManagerWindow(BaseResourceObject deviceService)
        {
            DeviceService = deviceService;
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            DataContext = DeviceService;
            ListViewService.ItemsSource = DeviceService.VisualChildren;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ListViewService_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
