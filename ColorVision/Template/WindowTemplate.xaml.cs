#pragma warning disable CA1707
using ColorVision.Extension;
using ColorVision.Util;
using cvColorVision;
using OpenCvSharp.Detail;
using ScottPlot.Styles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

namespace ColorVision.Template
{
    //Aoi检测部分


    public class ListConfig
    {
        public int ID { set; get; }
        public string Name { set; get; }

        public object? Value { set; get; }
    }

    public enum WindowTemplateType
    {
        AoiParam,
        Calibration,
        PGParam,
        LedReuslt,
        SxParm
    }





    /// <summary>
    /// WindowTemplate.xaml 的交互逻辑
    /// </summary>
    public partial class WindowTemplate : Window
    {
        WindowTemplateType WindowTemplateType { get; set; }

        public WindowTemplate(WindowTemplateType windowTemplateType)
        {
            WindowTemplateType = windowTemplateType;
            InitializeComponent();
        }
        public UserControl  UserControl { get; set; }
        public WindowTemplate(WindowTemplateType windowTemplateType,UserControl userControl)
        {
            WindowTemplateType = windowTemplateType;
            InitializeComponent();

            GridProperty.Children.Clear();
            UserControl = userControl;
            GridProperty.Children.Add(UserControl);
        }

        public new void Show()
        {
            base.Show();
            ListView1.SelectedIndex = 0;
        }


        public ObservableCollection<ListConfig> ListConfigs { get; set; } = new ObservableCollection<ListConfig>();
        private void Window_Initialized(object sender, EventArgs e)
        {
            ListConfigs = new ObservableCollection<ListConfig>();
            ListView1.ItemsSource = ListConfigs;
        }

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                switch (WindowTemplateType )
                {
                    case WindowTemplateType.AoiParam:
                    case WindowTemplateType.LedReuslt:
                    case WindowTemplateType.SxParm:
                        PropertyGrid1.SelectedObject = ListConfigs[listView.SelectedIndex].Value;
                        break;
                    case WindowTemplateType.Calibration:
                        if (UserControl is Calibration calibration && ListConfigs[listView.SelectedIndex].Value is CalibrationParam calibrationParam)
                        {
                            calibration.DataContext = calibrationParam;
                            calibration.CalibrationParam = calibrationParam;
                        }
                        break;
                    case WindowTemplateType.PGParam:
                        if (UserControl is PG pg && ListConfigs[listView.SelectedIndex].Value is PGParam pGparam)
                        {
                            pg.DataContext = pGparam;
                            pg.PGParam = pGparam;
                        }
                        break;
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (!TextBox1.Text.IsNullOrEmpty())
            {
                switch (WindowTemplateType)
                {
                    case WindowTemplateType.AoiParam:
                        CreateNewTemplate(TemplateControl.GetInstance().AoiParams, new AoiParam());
                        break;
                    case WindowTemplateType.Calibration:
                        CreateNewTemplate(TemplateControl.GetInstance().CalibrationParams, new CalibrationParam());
                        break;
                    case WindowTemplateType.PGParam:
                        CreateNewTemplate(TemplateControl.GetInstance().PGParams,new PGParam());
                        break;
                    case WindowTemplateType.LedReuslt:
                        CreateNewTemplate(TemplateControl.GetInstance().LedReusltParams, new LedReusltParam());
                        break;
                    case WindowTemplateType.SxParm:
                        CreateNewTemplate(TemplateControl.GetInstance().SxParms, new SxParm());
                        break;
                }
                TextBox1.Text =string.Empty;
            }
            else
            {
                MessageBox.Show("请输入模板名称", Application.Current.MainWindow.Title, MessageBoxButton.OK);
            }
        }

        private void CreateNewTemplate<T>(ObservableCollection<KeyValuePair<string, T>> keyValuePairs ,T t)
        {
            keyValuePairs.Add(new KeyValuePair<string, T>(TextBox1.Text, t));
            ListConfigs.Add(new ListConfig() { ID = ListConfigs.Count + 1, Name = TextBox1.Text, Value = t });
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (ListView1.SelectedIndex > -1)
            {
                if (MessageBox.Show($"是否删除模板{ListView1.SelectedIndex+1},删除后无法恢复!", Application.Current.MainWindow.Title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK)
                {
                    switch (WindowTemplateType)
                    {
                        case WindowTemplateType.AoiParam:
                            TemplateControl.GetInstance().AoiParams.RemoveAt(ListView1.SelectedIndex);
                            break;
                        case WindowTemplateType.Calibration:
                            TemplateControl.GetInstance().CalibrationParams.RemoveAt(ListView1.SelectedIndex);
                            break;
                        case WindowTemplateType.PGParam:
                            TemplateControl.GetInstance().PGParams.RemoveAt(ListView1.SelectedIndex);
                            break;
                        case WindowTemplateType.LedReuslt:
                            TemplateControl.GetInstance().LedReusltParams.RemoveAt(ListView1.SelectedIndex);
                            break;
                        case WindowTemplateType.SxParm:
                            TemplateControl.GetInstance().SxParms.RemoveAt(ListView1.SelectedIndex);
                            break;
                    }
                    ListConfigs.RemoveAt(ListView1.SelectedIndex);
                }
            }
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            TemplateControl.GetInstance().Save(WindowTemplateType);
            this.Close();
        }
    }
}
