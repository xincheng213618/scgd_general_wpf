using System.Windows;
using ProjectKB.Modbus;

namespace ProjectKB
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

        private void Test_Mesh_Click(object sender, RoutedEventArgs e)
        {
            nint a = MesDll.Testdll();
            var result = MesDll.PtrToString(a);
            MessageBox.Show(result);
        }

        private void Test_Mesh_Upload_Click(object sender, RoutedEventArgs e)
        {
            nint a = MesDll.CheckBL_WIP("1","!","1");
            var result = MesDll.PtrToString(a);
            MessageBox.Show(result);
        }
    }
}
