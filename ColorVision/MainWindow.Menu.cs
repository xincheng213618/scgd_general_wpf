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
                    case "AoiParam":
                        windowTemplate = new WindowTemplate(WindowTemplateType.AoiParam) { Title = "AOI参数设置" };
                        TemplateAbb(windowTemplate, TemplateControl.AoiParams);
                        break;
                    case "Calibration":
                        Calibration calibration = new Calibration(TemplateControl.CalibrationParams[0].Value);
                        windowTemplate = new WindowTemplate(WindowTemplateType.Calibration, calibration) { Title = "校正参数设置" };
                        TemplateAbb(windowTemplate, TemplateControl.CalibrationParams);
                        break;
                    case "PGParam":
                        PG pg = new PG(TemplateControl.PGParams[0].Value);
                        windowTemplate = new WindowTemplate(WindowTemplateType.PGParam, pg) { Title = "PG通讯设置" };
                        TemplateAbb(windowTemplate, TemplateControl.PGParams);
                        break;
                    case "LedReusltParams":
                        windowTemplate = new WindowTemplate(WindowTemplateType.LedReuslt) { Title = "数据判断模板设置" };
                        TemplateAbb(windowTemplate, TemplateControl.LedReusltParams);
                        break;
                    case "SxParms":
                        windowTemplate = new WindowTemplate(WindowTemplateType.SxParm) { Title = "源表模板设置" };
                        TemplateAbb(windowTemplate, TemplateControl.SxParams);
                        break;
                    case "FocusParm":
                        windowTemplate = new WindowTemplate(WindowTemplateType.PoiParam) { Title = "关注点设置" };
                        TemplateAbb(windowTemplate, TemplateControl.PoiParams);
                        break;
                    case "LedParam":
                        windowTemplate = new WindowTemplate(WindowTemplateType.LedParam) { Title = "灯珠检测模板" };
                        TemplateAbb(windowTemplate, TemplateControl.LedParams);
                        break;
                    case "FlowParam":
                        windowTemplate = new WindowTemplate(WindowTemplateType.FlowParam) { Title = "流程引擎" };
                        TemplateAbb(windowTemplate, TemplateControl.FlowParams);
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

        private void MenuItem9_Click(object sender, RoutedEventArgs e)
        {
            new FlowEngine.WindowFlowEngine() { Owner = this }.Show();
        }

        private void MenuItem_Exit(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }
    }
}
