using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace ColorVision.MQTT
{
    /// <summary>
    /// ServiceManager.xaml 的交互逻辑
    /// </summary>
    public partial class ServiceManager : Window
    {
        public ServiceManager()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            string processName = "CameraService"; // 替换为要关闭的进程的名称
            Process[] processes = Process.GetProcessesByName(processName);
            foreach (var item in processes)
            {
                if (item.ProcessName == "CameraService")
                {
                    ServiceDictionary.Add("CameraService",item);
                    break;
                }
            }
        }

        public static Dictionary<string, Process> ServiceDictionary { get; set; } = new Dictionary<string, Process>();

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                string exePath;
                switch (button.Tag.ToString())
                {
                    case "CameraService":
                        exePath = "CameraService.exe";
                        break;
                    case "SpectrumService":
                        exePath = "SpectrumService.exe";
                        break;
                    case "PGService":
                        exePath = "PGService.exe";
                        break;
                    case "Pss_SxService":
                        exePath = "Pss_SxService.exe";
                        break;
                    default:
                        return;
                }

                if (ServiceDictionary.TryGetValue(exePath, out Process process))
                {
                    ServiceDictionary.Remove(exePath);
                    process.Kill();
                    process.Close();
                    process.Dispose();
                    button.Content = "打开" + button.Tag.ToString();

                }
                else
                {
                    Process process1 = new Process();
                    process1.StartInfo.FileName = exePath;
                    process1.StartInfo.UseShellExecute = false;
                    process1.StartInfo.CreateNoWindow = true;
                    process1.Start();
                    ServiceDictionary.Add(exePath, process1);
                    button.Content ="关闭" + button.Tag.ToString();

  
                }
            }
        }


    }
}
