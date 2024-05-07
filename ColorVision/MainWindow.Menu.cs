using ColorVision.Common.Extension;
using ColorVision.Common.Utilities;
using ColorVision.UI.HotKey;
using ColorVision.Language;
using ColorVision.MQTT;
using ColorVision.MySql;
using ColorVision.Services.Flow;
using ColorVision.Services.RC;
using ColorVision.Settings;
using ColorVision.Themes;
using ColorVision.UI;
using log4net;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision
{


    /// <summary>
    /// 这里写的是菜单栏的事件
    /// </summary>
    public partial class MainWindow
    {

        private void TextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            new MQTTConnect() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        private void TextBlock_MouseLeftButtonDown1(object sender, MouseButtonEventArgs e)
        {
            new MySqlConnect() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        } 
        private void TextBlock_MouseLeftButtonDown_RC(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            new RCServiceConnect() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        private void LogF_Click(object sender, RoutedEventArgs e)
        {
            var fileAppender = (log4net.Appender.FileAppender)LogManager.GetRepository().GetAppenders().FirstOrDefault(a => a is log4net.Appender.FileAppender);
            if (fileAppender != null)
            {
                Process.Start("explorer.exe", $"{Path.GetDirectoryName(fileAppender.File)}");
            }
        }

        private void SettingF_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", $"{Path.GetDirectoryName(ConfigHandler.GetInstance().SoftwareConfigFileName)}");
        }



        private void Setting_Click(object sender, RoutedEventArgs e)
        {
            string fileName = ConfigHandler.GetInstance().SoftwareConfigFileName;
            bool result = Tool.HasDefaultProgram(fileName);
            if (!result)
                Process.Start(result ? "explorer.exe" : "notepad.exe", fileName);
        }
    }
}
