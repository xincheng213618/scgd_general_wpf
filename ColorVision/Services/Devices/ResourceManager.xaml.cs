using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ColorVision.Services.Devices
{
    public partial class ResourceManager : Window
    {
        public DeviceService DeviceService { get;set;}

        public ResourceManager(DeviceService deviceService)
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
