using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ColorVision.Engine.Services.PhyCameras
{
    public sealed class NameStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string name)
            {
                return string.IsNullOrWhiteSpace(name) ?  "没有配置相机ID" : name;
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("Converting from a string to a memory size is not supported.");
        }
    }

    /// <summary>
    /// PhyCameraManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class PhyCameraManagerWindow : Window
    {
        public PhyCameraManagerWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = PhyCameraManager.GetInstance();
            ServicesHelper.SelectAndFocusFirstNode(TreeView1);
            PhyCameraManager.GetInstance().Loaded +=(s,e) => ServicesHelper.SelectAndFocusFirstNode(TreeView1);
            PhyCameraManager.GetInstance().PhyCameras.CollectionChanged += (s,e)=> ServicesHelper.SelectAndFocusFirstNode(TreeView1);
        }

        private void TreeView1_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            StackPanelShow.Children.Clear();
            if (TreeView1.SelectedItem is PhyCamera phyCamera)
            {
                StackPanelShow.Children.Add(phyCamera.GetDeviceInfo());
            }
        }
    }
}
