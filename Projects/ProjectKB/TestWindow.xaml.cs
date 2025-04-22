using System.Windows;
using ProjectARVR.Modbus;

namespace ProjectARVR
{
    /// <summary>
    /// TestWindow.xaml 的交互逻辑
    /// </summary>
    public partial class TestWindow : Window
    {
        public TestWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ModbusControl.TestConnect(ModbusControl.Config);
        }

        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            ModbusControl.GetInstance().SetRegisterValue(1);
        }
    }
}
