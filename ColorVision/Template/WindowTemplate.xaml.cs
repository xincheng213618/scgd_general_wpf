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
using System.IO;
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
        SxParm,
        PoiParam,
        LedParam,
        FlowParam
    }





    /// <summary>
    /// WindowTemplate.xaml 的交互逻辑
    /// </summary>
    public partial class WindowTemplate : Window
    {
        WindowTemplateType TemplateType { get; set; }

        public WindowTemplate(WindowTemplateType windowTemplateType)
        {
            TemplateType = windowTemplateType;
            InitializeComponent();


            switch (TemplateType)
            {
                case WindowTemplateType.LedParam:
                case WindowTemplateType.FlowParam:
                case WindowTemplateType.PoiParam:

                    GridProperty.Visibility = Visibility.Collapsed;
                    Grid.SetColumnSpan(TemplateGrid, 2);
                    Grid.SetRowSpan(TemplateGrid, 1);

                    Grid.SetColumnSpan(CreateGrid, 2);
                    Grid.SetColumn(CreateGrid, 0);


                    this.MinWidth = 400;
                    this.Width = 450;
                    break;
                default:
                    break;
            }


        }
        public UserControl  UserControl { get; set; }
        public WindowTemplate(WindowTemplateType windowTemplateType,UserControl userControl)
        {
            TemplateType = windowTemplateType;
            InitializeComponent();

            GridProperty.Children.Clear();

            UserControl = userControl;
            GridProperty.Children.Add(UserControl);
        }


        public new void ShowDialog()
        {
            switch (TemplateType)
            {
                case WindowTemplateType.LedParam:
                case WindowTemplateType.PoiParam:
                    break;
                case WindowTemplateType.FlowParam:
                    TemplateGrid.Header = "流程";
                    Button button = new Button() { Content = "导入流程", Width =80};
                    button.Click += (s, e) =>
                    {
                        System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
                        ofd.Filter = "*.stn|*.stn";
                        if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                        CreateNewTemplate(TemplateControl.GetInstance().FlowParams, Path.GetFileNameWithoutExtension(ofd.FileName), new FlowParam() { FileName = ofd.FileName });
                    };
                    FunctionGrid.Children.Insert(1, button);
                    FunctionGrid.Columns = 4;
                    FunctionGrid.Width = 400;
                    break;
                default:
                    ListView1.SelectedIndex = 0;
                    break;
            }
            base.ShowDialog();

        }


        public ObservableCollection<ListConfig> ListConfigs { get; set; } = new ObservableCollection<ListConfig>();
        private void Window_Initialized(object sender, EventArgs e)
        {
            ListConfigs = new ObservableCollection<ListConfig>();
            ListView1.ItemsSource = ListConfigs;
        }


        private void ListView1_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                switch (TemplateType)
                {
                    case WindowTemplateType.PoiParam:
                        new WindowFocusPoint() { Owner = Application.Current.MainWindow }.Show();
                        break;
                    case WindowTemplateType.LedParam:
                        new WindowLedCheck() { Owner = Application.Current.MainWindow }.Show();
                        break;
                    case WindowTemplateType.FlowParam:
                        if (ListConfigs[listView.SelectedIndex].Value is FlowParam flowParam)
                        {
                            flowParam.FileName ??= ListConfigs[listView.SelectedIndex].Name;
                            new FlowEngine.WindowFlowEngine(flowParam) { Owner = Application.Current.MainWindow }.Show();
                        }
                        break;
                }
            }
        }

        //这里追加逻辑，如果是打开一个新的窗口，则原有的选中取消

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                switch (TemplateType)
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
                switch (TemplateType)
                {
                    case WindowTemplateType.AoiParam:
                        CreateNewTemplate(TemplateControl.GetInstance().AoiParams, TextBox1.Text, new AoiParam());
                        break;
                    case WindowTemplateType.Calibration:
                        CreateNewTemplate(TemplateControl.GetInstance().CalibrationParams, TextBox1.Text, new CalibrationParam());
                        break;
                    case WindowTemplateType.PGParam:
                        CreateNewTemplate(TemplateControl.GetInstance().PGParams,TextBox1.Text, new PGParam());
                        break;
                    case WindowTemplateType.LedReuslt:
                        CreateNewTemplate(TemplateControl.GetInstance().LedReusltParams, TextBox1.Text, new LedReusltParam());
                        break;
                    case WindowTemplateType.SxParm:
                        CreateNewTemplate(TemplateControl.GetInstance().SxParams, TextBox1.Text, new SxParam());
                        break;
                    case WindowTemplateType.PoiParam:
                        CreateNewTemplate(TemplateControl.GetInstance().PoiParams, TextBox1.Text, new PoiParam());
                        break;
                    case WindowTemplateType.LedParam:
                        CreateNewTemplate(TemplateControl.GetInstance().LedParams, TextBox1.Text, new LedParam());
                        break;
                    case WindowTemplateType.FlowParam:
                        CreateNewTemplate(TemplateControl.GetInstance().FlowParams, TextBox1.Text, new FlowParam() {FileName = TextBox1.Text });
                        break;

                }
                TextBox1.Text =string.Empty;
            }
            else
            {
                MessageBox.Show("请输入模板名称", Application.Current.MainWindow.Title, MessageBoxButton.OK);
            }
        }

        private void CreateNewTemplate<T>(ObservableCollection<KeyValuePair<string, T>> keyValuePairs ,string Name,T t)
        {
            keyValuePairs.Add(new KeyValuePair<string, T>(Name, t));
            ListConfigs.Add(new ListConfig() { ID = ListConfigs.Count + 1, Name = Name, Value = t });
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (ListView1.SelectedIndex > -1)
            {
                if (MessageBox.Show($"是否删除模板{ListView1.SelectedIndex+1},删除后无法恢复!", Application.Current.MainWindow.Title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK)
                {
                    switch (TemplateType)
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
                            TemplateControl.GetInstance().SxParams.RemoveAt(ListView1.SelectedIndex);
                            break;
                        case WindowTemplateType.PoiParam:
                            TemplateControl.GetInstance().FocusParms.RemoveAt(ListView1.SelectedIndex);
                            break;
                        case WindowTemplateType.FlowParam:
                            TemplateControl.GetInstance().FlowParams.RemoveAt(ListView1.SelectedIndex);
                            break;
                    }
                    ListConfigs.RemoveAt(ListView1.SelectedIndex);
                }
            }
            else
            {
                MessageBox.Show("请先选择" + TemplateGrid.Header);
            }
        }





        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            TemplateControl.GetInstance().Save(TemplateType);
            this.Close();
        }

        private void ListView1_Selected(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("2222");
        }


    }
}
