using ColorVision.Extension;
using ColorVision.MVVM;
using ColorVision.MySql;
using ColorVision.MySql.DAO;
using ColorVision.SettingUp;
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


    public class ListConfig:ViewModelBase
    {
        public int ID { get; set; }
        public string Name {  get; set; }
        public string Tag { get => _Tag; set { _Tag = value;NotifyPropertyChanged();} }
        private string _Tag;


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
        FlowParam
    }





    /// <summary>
    /// WindowTemplate.xaml 的交互逻辑
    /// </summary>
    public partial class WindowTemplate : Window
    {
        WindowTemplateType TemplateType { get; set; }
        TemplateControl TemplateControl { get;set; }
        public WindowTemplate(WindowTemplateType windowTemplateType)
        {
            TemplateType = windowTemplateType;
            TemplateControl = TemplateControl.GetInstance();
            InitializeComponent();


            switch (TemplateType)
            {
                case WindowTemplateType.FlowParam:
                case WindowTemplateType.PoiParam:

                    GridProperty.Visibility = Visibility.Collapsed;
                    Grid.SetColumnSpan(TemplateGrid, 2);
                    Grid.SetRowSpan(TemplateGrid, 1);

                    Grid.SetColumnSpan(CreateGrid, 2);
                    Grid.SetColumn(CreateGrid, 0);
                    break;
                default:
                    break;
            }


        }
        public UserControl  UserControl { get; set; }
        public WindowTemplate(WindowTemplateType windowTemplateType,UserControl userControl)
        {
            TemplateType = windowTemplateType;
            TemplateControl = TemplateControl.GetInstance();
            InitializeComponent();

            GridProperty.Children.Clear();

            UserControl = userControl;
            GridProperty.Children.Add(UserControl);
        }


        public new void ShowDialog()
        {
            switch (TemplateType)
            {
                case WindowTemplateType.PoiParam:
                    TemplateGrid.Header = "点集";
                    this.MinWidth = 390;
                    this.Width = 390;
                    break;
                case WindowTemplateType.FlowParam:
                    TemplateGrid.Header = "流程";
                    Button button = new Button() { Content = "导入流程", Width =80};
                    button.Click += (s, e) =>
                    {
                        System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
                        ofd.Filter = "*.stn|*.stn";
                        if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                        CreateNewTemplate(TemplateControl.FlowParams, Path.GetFileNameWithoutExtension(ofd.FileName), new FlowParam() { FileName = ofd.FileName });
                    };
                    FunctionGrid.Children.Insert(2, button);

                    Button button1 = new Button() { Content = "导出流程", Width = 80 };
                    button1.Click += (s, e) =>
                    {
                        if (ListView1.SelectedIndex<0)
                        {
                            MessageBox.Show("请选择您要导出的流程");
                            return;
                        }
                        System.Windows.Forms.SaveFileDialog ofd = new System.Windows.Forms.SaveFileDialog();
                        ofd.DefaultExt = "stn";
                        ofd.Filter = "*.stn|*.stn";
                        ofd.AddExtension = false;
                        ofd.RestoreDirectory = true;
                        ofd.Title = "导出流程";
                        ofd.FileName = TemplateControl.FlowParams[ListView1.SelectedIndex].Key;
                        if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                        CreateNewTemplate(TemplateControl.FlowParams, Path.GetFileNameWithoutExtension(ofd.FileName), new FlowParam() { FileName = ofd.FileName });
                    };
                    FunctionGrid.Children.Insert(3, button1);

                    FunctionGrid.Columns = 5;
                    FunctionGrid.Width = 450;
                    this.MinWidth = 400;
                    this.Width = 500;
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
                        if (ListConfigs[listView.SelectedIndex].Value is PoiParam poiParam)
                        {
                            var WindowFocusPoint = new WindowFocusPoint(poiParam) { Owner = this };
                            WindowFocusPoint.Closed += async (s, e) =>
                            {
                                await Task.Delay(30);
                                ListConfigs[listView.SelectedIndex].Tag = $"{poiParam.Width}*{poiParam.Height}{(GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql ? "" : $"_{poiParam.PoiPoints.Count}")}";
                            };
                            WindowFocusPoint.Show();
                        }
                        break;
                    case WindowTemplateType.FlowParam:
                        if (ListConfigs[listView.SelectedIndex].Value is FlowParam flowParam)
                        {
                            flowParam.Name ??= ListConfigs[listView.SelectedIndex].Name;
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
        public string NewCreateFileName(string FileName)
        {
            List<string> Names = new List<string>();
            foreach (var item in ListConfigs)
            {
                Names.Add(item.Name);
            }
            for (int i = 1; i < 9999; i++)
            {
                if (!Names.Contains($"{FileName}{i}"))
                    return $"{FileName}{i}";
            }
            return FileName;
        }

        private void CreateNewTemplateFromDB()
        {
            switch (TemplateType)
            {
                case WindowTemplateType.AoiParam:
                    AoiParam? aoiParam = TemplateControl.AddAoiParam(TextBox1.Text);
                    if (aoiParam != null) CreateNewTemplate(TemplateControl.AoiParams, TextBox1.Text, aoiParam);
                    else MessageBox.Show("数据库创建AOI模板失败");
                    break;
                case WindowTemplateType.Calibration:
                    CreateNewTemplate(TemplateControl.CalibrationParams, TextBox1.Text, new CalibrationParam());
                    break;
                case WindowTemplateType.PGParam:
                    CreateNewTemplate(TemplateControl.PGParams, TextBox1.Text, new PGParam());
                    break;
                case WindowTemplateType.LedReuslt:
                    CreateNewTemplate(TemplateControl.LedReusltParams, TextBox1.Text, new LedReusltParam());
                    break;
                case WindowTemplateType.SxParm:
                    CreateNewTemplate(TemplateControl.SxParams, TextBox1.Text, new SxParam());
                    break;
                case WindowTemplateType.PoiParam:
                        PoiParam? poiParam = TemplateControl.AddPoiParam(TextBox1.Text);
                        if (poiParam != null) CreateNewTemplate(TemplateControl.PoiParams, TextBox1.Text, poiParam);
                        else MessageBox.Show("数据库创建POI模板失败");
                    break;
                case WindowTemplateType.FlowParam:
                        FlowParam? flowParam = TemplateControl.AddFlowParam(TextBox1.Text);
                        if (flowParam != null) CreateNewTemplate(TemplateControl.FlowParams, TextBox1.Text, flowParam);
                        else MessageBox.Show("数据库创建流程模板失败");
                    break;

            }
        }

        private void CreateNewTemplateFromCsv()
        {
            switch (TemplateType)
            {
                case WindowTemplateType.AoiParam:
                    CreateNewTemplate(TemplateControl.AoiParams, TextBox1.Text, new AoiParam());
                    break;
                case WindowTemplateType.Calibration:
                    CreateNewTemplate(TemplateControl.CalibrationParams, TextBox1.Text, new CalibrationParam());
                    break;
                case WindowTemplateType.PGParam:
                    CreateNewTemplate(TemplateControl.PGParams, TextBox1.Text, new PGParam());
                    break;
                case WindowTemplateType.LedReuslt:
                    CreateNewTemplate(TemplateControl.LedReusltParams, TextBox1.Text, new LedReusltParam());
                    break;
                case WindowTemplateType.SxParm:
                    CreateNewTemplate(TemplateControl.SxParams, TextBox1.Text, new SxParam());
                    break;
                case WindowTemplateType.PoiParam:
                    CreateNewTemplate(TemplateControl.PoiParams, TextBox1.Text , new PoiParam() { });
                    break;
                case WindowTemplateType.FlowParam:
                    CreateNewTemplate(TemplateControl.FlowParams, TextBox1.Text, new FlowParam() { Name = TextBox1.Text });
                    break;
            }
        }

        private void Button_New_Click(object sender, RoutedEventArgs e)
        {
            if (!TextBox1.Text.IsNullOrEmpty())
            {
                if (GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql)
                {
                    CreateNewTemplateFromDB();
                }
                else
                {
                    CreateNewTemplateFromCsv();
                }
                TextBox1.Text = NewCreateFileName("default");
            }
            else
            {
                MessageBox.Show("请输入模板名称", Application.Current.MainWindow.Title, MessageBoxButton.OK);
            }
        }

        private void CreateNewTemplate<T>(ObservableCollection<KeyValuePair<string, T>> keyValuePairs ,string Name,T t )
        {
            keyValuePairs.Add(new KeyValuePair<string, T>(Name, t));
            ListConfig config = new ListConfig() { ID = ListConfigs.Count + 1, Name = Name, Value = t  };
            ListConfigs.Add(config);
            ListView1.SelectedIndex = ListConfigs.Count - 1;
            ListView1.ScrollIntoView(config);
        }

        private void Button_Del_Click(object sender, RoutedEventArgs e)
        {
            if (ListView1.SelectedIndex > -1)
            {
                if (MessageBox.Show($"是否删除模板{ListView1.SelectedIndex+1},删除后无法恢复!", Application.Current.MainWindow.Title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK)
                {
                    switch (TemplateType)
                    {
                        case WindowTemplateType.AoiParam:
                            if (GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql)
                                TemplateControl.PoiMasterDeleteById(TemplateControl.AoiParams[ListView1.SelectedIndex].Value.ID);
                            TemplateControl.AoiParams.RemoveAt(ListView1.SelectedIndex);
                            break;
                        case WindowTemplateType.Calibration:
                            TemplateControl.CalibrationParams.RemoveAt(ListView1.SelectedIndex);
                            break;
                        case WindowTemplateType.PGParam:
                            TemplateControl.PGParams.RemoveAt(ListView1.SelectedIndex);
                            break;
                        case WindowTemplateType.LedReuslt:
                            TemplateControl.LedReusltParams.RemoveAt(ListView1.SelectedIndex);
                            break;
                        case WindowTemplateType.SxParm:
                            TemplateControl.SxParams.RemoveAt(ListView1.SelectedIndex);
                            break;
                        case WindowTemplateType.PoiParam:
                            if (GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql)
                                TemplateControl.PoiMasterDeleteById(TemplateControl.PoiParams[ListView1.SelectedIndex].Value.ID);
                            TemplateControl.PoiParams.RemoveAt(ListView1.SelectedIndex);
                            break;
                        case WindowTemplateType.FlowParam:
                            if (GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql)
                                TemplateControl.FlowMasterDeleteById(TemplateControl.FlowParams[ListView1.SelectedIndex].Value.ID);
                            TemplateControl.FlowParams.RemoveAt(ListView1.SelectedIndex);
                            break;
                    }
                    ListConfigs.RemoveAt(ListView1.SelectedIndex);
                    ListView1.SelectedIndex = ListConfigs.Count - 1;
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

        private void Button_Save_Click(object sender, RoutedEventArgs e)
        {
            TemplateControl.Save(TemplateType);
            this.Close();
        }

        private void ListView1_Loaded(object sender, RoutedEventArgs e)
        {
            TextBox1.Text = NewCreateFileName("default");
        }
    }
}
