using ColorVision.Template;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;


namespace ColorVision
{
    /// <summary>
    /// 这里写的是菜单栏的事件
    /// </summary>
    public partial class MainWindow
    {
        TemplateControl TemplateControl { get; set; }

        private void MenuItem_Template(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                WindowTemplate windowTemplate;
                switch (menuItem.Tag.ToString())
                {
                    case "AOI参数设置":
                        windowTemplate = new WindowTemplate(WindowTemplateType.AoiParam) { Title = "AOI参数设置" };
                        TemplateAbb(windowTemplate, TemplateControl.AoiParams);
                        break;
                    case "校正参数设置":
                        Calibration calibration = new Calibration(TemplateControl.CalibrationParams[0].Value);
                        windowTemplate = new WindowTemplate(WindowTemplateType.Calibration, calibration) { Title = "校正参数设置" };
                        TemplateAbb(windowTemplate, TemplateControl.CalibrationParams);
                        break;
                    case "通讯设置":
                        PG pg = new PG(TemplateControl.PGParams[0].Value);
                        windowTemplate = new WindowTemplate(WindowTemplateType.PGParam, pg) { Title = "PG通讯设置" };
                        TemplateAbb(windowTemplate, TemplateControl.PGParams);
                        break;
                    case "数据判断模板设置":
                        windowTemplate = new WindowTemplate(WindowTemplateType.LedReuslt) { Title = "数据判断模板设置" };
                        TemplateAbb(windowTemplate, TemplateControl.LedReusltParams);
                        break;
                    case "源表模板设置":
                        windowTemplate = new WindowTemplate(WindowTemplateType.SxParm) { Title = "源表模板设置" };
                        TemplateAbb(windowTemplate, TemplateControl.SxParms);
                        break;
                    default:
                        break;
                }
            }
        }

        private void TemplateAbb<T>(WindowTemplate windowTemplate, ObservableCollection<KeyValuePair<string, T>> keyValuePairs)
        {
            windowTemplate.Owner = this;
            int id = 1;
            windowTemplate.ListConfigs.Clear();
            foreach (var item in keyValuePairs)
            {
                ListConfig listConfig = new ListConfig();
                listConfig.ID = id++;
                listConfig.Name = item.Key;
                listConfig.Value = item.Value;
                windowTemplate.ListConfigs.Add(listConfig);
            }
            windowTemplate.ListView1.SelectedIndex = 0;
            windowTemplate.ShowDialog();
        }

        private void MenuItem_Click_7(object sender, RoutedEventArgs e)
        {
            new WindowORM().Show();
        }


        private void MenuItem_Click8(object sender, RoutedEventArgs e)
        {
            new WindowFourColorCalibration() {Owner = this}.Show();
        }

        private void MenuItem_Click_9(object sender, RoutedEventArgs e)
        {
            new WindowFocusPoint() { Owner = this }.Show();
        }

        private void MenuItem9_Click(object sender, RoutedEventArgs e)
        {
            FlowEngine.WindowFlowEngine windowFlowEngine;
            windowFlowEngine = new FlowEngine.WindowFlowEngine();
            windowFlowEngine.Owner = this;
            windowFlowEngine.Show();
        }



        private void MenuItem_Click_10(object sender, RoutedEventArgs e)
        {
            WindowLedCheck windowLedCheck = new WindowLedCheck();
            windowLedCheck.Show();
        }


        private void MenuItem_Exit(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }
    }
}
