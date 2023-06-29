
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision
{
    /// <summary>
    /// AboutMsg.xaml 的交互逻辑
    /// </summary>
    public partial class AboutMsgWindow : Window
    {
        public AboutMsgWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.Topmost = true;
            CloseButton.Focus();
#if (DEBUG == true)
            TextBlockVision.Text = $"ColorVision{(DebugBuild(Assembly.GetExecutingAssembly())? " (Debug) " : "(Release)")}{(Debugger.IsAttached ? " (调试中) " : "")} ({(IntPtr.Size == 4 ? "32" : "64")}位) -  {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}";
#else
            TextBlockVision.Text = $"ColorVision{(DebugBuild(Assembly.GetExecutingAssembly()) ? " (Debug)" : "")}{(Debugger.IsAttached ? " (调试中) " : "")} ({(IntPtr.Size == 4 ? "32" : "64")}位) -  {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}";
#endif
            Icon = null;

        }

        private static bool DebugBuild(Assembly assembly)
        {
            foreach (object attribute in assembly.GetCustomAttributes(false))
            {
                if (attribute is DebuggableAttribute)
                {
                    DebuggableAttribute _attribute = attribute as DebuggableAttribute;

                    return _attribute.IsJITTrackingEnabled;
                }
            }
            return false;
        }


        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Image_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Process.Start("explorer", "https://www.color-vision.com.cn/");
            this.Close();
        }
    }
}
