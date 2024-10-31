using ColorVision.Engine.Services.Core;
using ColorVision.Themes;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.PhyCameras.Group
{
    public partial class ResourceManagerWindow : Window
    {
        public ServiceObjectBase DeviceService { get;set;}

        public ResourceManagerWindow(ServiceObjectBase deviceService)
        {
            DeviceService = deviceService;
            InitializeComponent();
            this.ApplyCaption();
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
