using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ColorVision.Engine.Batch
{
    public class TypeNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.GetType().Name ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    /// <summary>
    /// BatchProcessManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class BatchProcessManagerWindow : Window
    {
        public BatchProcessManagerWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {

        }
    }
}
