using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Jsons.KB;
using log4net;
using ProjectKB.Modbus;
using System.Windows;

namespace ProjectKB
{
    /// <summary>
    /// TestWindow.xaml 的交互逻辑
    /// </summary>
    public partial class TestWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(TestWindow));

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
            IntPtr a = MesDll.Testdll();
            var result = MesDll.PtrToString(a);
            MessageBox.Show(result);
        }

        private void Test_Mesh_Upload_Click(object sender, RoutedEventArgs e)
        {
            log.Info($"CheckWIP Stage{SummaryManager.GetInstance().Summary.Stage},SN:{ProjectKBConfig.Instance.SN}");
            IntPtr a = MesDll.CheckWIP(SummaryManager.GetInstance().Summary.Stage,ProjectKBConfig.Instance.SN);
      
            var result = MesDll.PtrToString(a);
            log.Info(result);
            MessageBox.Show(result);
        }

        private void Test_CheckCollect_test_Click(object sender, RoutedEventArgs e)
        {
            var Summary = SummaryManager.GetInstance().Summary;
            log.Info($"CheckWIP Stage{Summary.Stage},SN:{ProjectKBConfig.Instance.SN}");
            IntPtr a = MesDll.Collect_test(Summary.Stage, ProjectKBConfig.Instance.SN, "N", Summary.MachineNO,Summary.LineNO, "NG", "NG", "NG");

            var result = MesDll.PtrToString(a);
            log.Info(result);
            MessageBox.Show(result);
        }
    }
}
