using ColorVision.Extension;
using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using ColorVision.SettingUp;
using ColorVision.Template.Algorithm;
using ColorVision.Util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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




    /// <summary>
    /// WindowTemplate.xaml 的交互逻辑
    /// </summary>
    public partial class WindowTemplate : Window
    {
        TemplateType TemplateType { get; set; }
        TemplateControl TemplateControl { get;set; }
        public WindowTemplate(TemplateType windowTemplateType)
        {
            TemplateType = windowTemplateType;
            TemplateControl = TemplateControl.GetInstance();
            InitializeComponent();


            switch (TemplateType)
            {
                case TemplateType.FlowParam:
                case TemplateType.PoiParam:

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
        public WindowTemplate(TemplateType windowTemplateType,UserControl userControl)
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
                case TemplateType.PoiParam:
                    TemplateGrid.Header = "点集";
                    this.MinWidth = 390;
                    this.Width = 390;
                    this.Closed += (s, e) =>
                    {
                        TemplateControl.Save(TemplateType);
                    };
                    break;
                case TemplateType.FlowParam:
                    TemplateGrid.Header = "流程";
                    Button button = new Button() { Content = "导入流程", Width =80};
                    button.Click += (s, e) =>
                    {
                        System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
                        ofd.Filter = "*.stn|*.stn";
                        if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                        //CreateNewTemplate(TemplateControl.FlowParams, Path.GetFileNameWithoutExtension(ofd.FileName), new FlowParam() { FileName = ofd.FileName });
                        string name = Path.GetFileNameWithoutExtension(ofd.FileName);
                        FlowParam? flowParam = TemplateControl.AddFlowParam(name);
                        if (flowParam != null)
                        {
                            flowParam.FileName = Path.GetFileName(ofd.FileName); ;
                            CreateNewTemplate(TemplateControl.FlowParams, name, flowParam);

                            TemplateControl.GetInstance().Save2DB(flowParam);
                        }
                        else MessageBox.Show("数据库创建流程模板失败");
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

                        SaveAsFile(ofd.FileName,TemplateControl.FlowParams[ListView1.SelectedIndex].Value);
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
        private static void SaveAsFile(string sFileName,  FlowParam flow)
        {
            Tool.Base64ToFile(flow.DataBase64, sFileName);
        }
        public ObservableCollection<ListConfig> ListConfigs { get; set; } = new ObservableCollection<ListConfig>();
        private void Window_Initialized(object sender, EventArgs e)
        {
            //ListConfigs = new ObservableCollection<ListConfig>();
            ListView1.ItemsSource = ListConfigs;
        }


        private void ListView1_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                switch (TemplateType)
                {
                    case TemplateType.PoiParam:
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
                    case TemplateType.FlowParam:
                        if (ListConfigs[listView.SelectedIndex].Value is FlowParam flowParam)
                        {
                            flowParam.Name ??= ListConfigs[listView.SelectedIndex].Name;
                            new WindowFlowEngine(flowParam) { Owner = Application.Current.MainWindow }.Show();
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
                    case TemplateType.Calibration:
                        if (UserControl is Calibration calibration && ListConfigs[listView.SelectedIndex].Value is CalibrationParam calibrationParam)
                        {
                            calibration.DataContext = calibrationParam;
                            calibration.CalibrationParam = calibrationParam;
                        }
                        break;
                    case TemplateType.MeasureParm:
                        if (UserControl is MeasureParamControl mpc && ListConfigs[listView.SelectedIndex].Value is MeasureParam mp)
                        {
                            mpc.MasterID = mp.ID;
                            List<MeasureDetailModel> des = TemplateControl.LoadMeasureDetail(mp.ID);
                            mpc.reload(des);
                            mpc.ModTypeConfigs.Clear();
                            mpc.ModTypeConfigs.Add(new MParamConfig(-1,"关注点","POI"));
                            List<SysModMasterModel> sysModMaster = TemplateControl.LoadSysModMaster();
                            foreach (SysModMasterModel model in sysModMaster)
                            {
                                mpc.ModTypeConfigs.Add(new MParamConfig(model));
                            }
                        }
                        break;
                    default:
                        PropertyGrid1.SelectedObject = ListConfigs[listView.SelectedIndex].Value;
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

                case TemplateType.Calibration:
                    CreateNewTemplate(TemplateControl.CalibrationParams, TextBox1.Text, new CalibrationParam());
                    break;
                case TemplateType.LedReuslt:
                    CreateNewTemplate(TemplateControl.LedReusltParams, TextBox1.Text, new LedReusltParam());
                    break;
                case TemplateType.AoiParam:
                    AoiParam? aoiParam = TemplateControl.AddParamMode<AoiParam>(TemplateTypeFactory.GetModeTemplateType(TemplateType), TextBox1.Text);
                    if (aoiParam != null) CreateNewTemplate(TemplateControl.AoiParams, TextBox1.Text, aoiParam);
                    else MessageBox.Show("数据库创建AOI模板失败");
                    break;
                case TemplateType.PGParam:
                    PGParam? pgParam = TemplateControl.AddParamMode<PGParam>(TemplateTypeFactory.GetModeTemplateType(TemplateType), TextBox1.Text);
                    if (pgParam != null) CreateNewTemplate(TemplateControl.PGParams, TextBox1.Text, pgParam);
                    else MessageBox.Show("数据库创建PG模板失败");
                    break;

                case TemplateType.SMUParam:
                    SMUParam?  sMUParam = TemplateControl.AddParamMode<SMUParam>(TemplateTypeFactory.GetModeTemplateType(TemplateType), TextBox1.Text);
                    if (sMUParam != null) CreateNewTemplate(TemplateControl.SMUParams, TextBox1.Text, sMUParam);
                    else MessageBox.Show("数据库创建源表模板失败");
                    break;
                case TemplateType.MTFParam:
                    MTFParam? mTFParam = TemplateControl.AddParamMode<MTFParam>(TemplateTypeFactory.GetModeTemplateType(TemplateType), TextBox1.Text);
                    if (mTFParam != null) CreateNewTemplate(TemplateControl.MTFParams, TextBox1.Text, mTFParam);
                    else MessageBox.Show("数据库创建MTF模板失败");
                    break;
                case TemplateType.SFRParam:
                    SFRParam? sFRParam = TemplateControl.AddParamMode<SFRParam>(TemplateTypeFactory.GetModeTemplateType(TemplateType), TextBox1.Text);
                    if (sFRParam != null) CreateNewTemplate(TemplateControl.SFRParams, TextBox1.Text, sFRParam);
                    else MessageBox.Show("数据库创建MTF模板失败"); break;
                case TemplateType.FOVParam:
                    FOVParam? fOVParam = TemplateControl.AddParamMode<FOVParam>(TemplateTypeFactory.GetModeTemplateType(TemplateType), TextBox1.Text);
                    if (fOVParam != null) CreateNewTemplate(TemplateControl.FOVParams, TextBox1.Text, fOVParam);
                    else MessageBox.Show("数据库创建FOV模板失败"); break;
                case TemplateType.GhostParam:
                    GhostParam? ghostParam = TemplateControl.AddParamMode<GhostParam>(TemplateTypeFactory.GetModeTemplateType(TemplateType), TextBox1.Text);
                    if (ghostParam != null) CreateNewTemplate(TemplateControl.GhostParams, TextBox1.Text, ghostParam);
                    else MessageBox.Show("数据库创建Ghost模板失败"); break;
                case TemplateType.DistortionParam:
                    DistortionParam? distortionParam = TemplateControl.AddParamMode<DistortionParam>(TemplateTypeFactory.GetModeTemplateType(TemplateType), TextBox1.Text);
                    if (distortionParam != null) CreateNewTemplate(TemplateControl.DistortionParams, TextBox1.Text, distortionParam);
                    else MessageBox.Show("数据库创建Distortion模板失败"); break;
                case TemplateType.PoiParam:
                    PoiParam? poiParam = TemplateControl.AddPoiParam(TextBox1.Text);
                    if (poiParam != null) CreateNewTemplate(TemplateControl.PoiParams, TextBox1.Text, poiParam);
                    else MessageBox.Show("数据库创建POI模板失败");
                    break;
                case TemplateType.FlowParam:
                    FlowParam? flowParam = TemplateControl.AddFlowParam(TextBox1.Text);
                    if (flowParam != null) CreateNewTemplate(TemplateControl.FlowParams, TextBox1.Text, flowParam);
                    else MessageBox.Show("数据库创建流程模板失败");
                    break;
                case TemplateType.MeasureParm:
                    MeasureParam? measureParam = TemplateControl.AddMeasureParam(TextBox1.Text);
                    if (measureParam != null) CreateNewTemplate(TemplateControl.MeasureParams, TextBox1.Text, measureParam);
                    else MessageBox.Show("数据库创建流程模板失败");
                    break;
            }
        }

        private void CreateNewTemplateFromCsv()
        {
            switch (TemplateType)
            {
                case TemplateType.AoiParam:
                    CreateNewTemplate(TemplateControl.AoiParams, TextBox1.Text, new AoiParam());
                    break;
                case TemplateType.Calibration:
                    CreateNewTemplate(TemplateControl.CalibrationParams, TextBox1.Text, new CalibrationParam());
                    break;
                case TemplateType.PGParam:
                    CreateNewTemplate(TemplateControl.PGParams, TextBox1.Text, new PGParam());
                    break;
                case TemplateType.LedReuslt:
                    CreateNewTemplate(TemplateControl.LedReusltParams, TextBox1.Text, new LedReusltParam());
                    break;
                case TemplateType.SMUParam:
                    CreateNewTemplate(TemplateControl.SMUParams, TextBox1.Text, new SMUParam());
                    break;
                case TemplateType.MTFParam:
                    CreateNewTemplate(TemplateControl.MTFParams, TextBox1.Text, new MTFParam() { });
                    break;
                case TemplateType.SFRParam:
                    CreateNewTemplate(TemplateControl.SFRParams, TextBox1.Text, new SFRParam() { });
                    break;
                case TemplateType.FOVParam:
                    CreateNewTemplate(TemplateControl.FOVParams, TextBox1.Text, new FOVParam() { });
                    break;
                case TemplateType.GhostParam:
                    CreateNewTemplate(TemplateControl.GhostParams, TextBox1.Text, new GhostParam() { });
                    break;
                case TemplateType.DistortionParam:
                    CreateNewTemplate(TemplateControl.DistortionParams, TextBox1.Text, new DistortionParam() { });
                    break;
                case TemplateType.PoiParam:
                    CreateNewTemplate(TemplateControl.PoiParams, TextBox1.Text , new PoiParam() { });
                    break;
                case TemplateType.FlowParam:
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
            void TemplateDel<T>(ObservableCollection<KeyValuePair<string, T>> keyValuePairs) where T : ParamBase
            {
                if (GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql)
                    TemplateControl.ModMasterDeleteById(keyValuePairs[ListView1.SelectedIndex].Value.ID);
                keyValuePairs.RemoveAt(ListView1.SelectedIndex);
            }

            if (ListView1.SelectedIndex > -1)
            {
                if (MessageBox.Show($"是否删除模板{ListView1.SelectedIndex+1},删除后无法恢复!", Application.Current.MainWindow.Title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK)
                {
                    switch (TemplateType)
                    {
                        case TemplateType.AoiParam:
                            TemplateDel(TemplateControl.AoiParams);
                            break;
                        case TemplateType.Calibration:
                            TemplateControl.CalibrationParams.RemoveAt(ListView1.SelectedIndex);
                            break;
                        case TemplateType.PGParam:
                            TemplateDel(TemplateControl.PGParams);
                            break;
                        case TemplateType.LedReuslt:
                            TemplateControl.LedReusltParams.RemoveAt(ListView1.SelectedIndex);
                            break;
                        case TemplateType.SMUParam:
                            TemplateDel(TemplateControl.SMUParams);
                            break;
                        case TemplateType.PoiParam:
                            TemplateDel(TemplateControl.PoiParams);
                            break;
                        case TemplateType.MTFParam:
                            TemplateDel(TemplateControl.MTFParams);
                            break;
                        case TemplateType.SFRParam:
                            TemplateDel(TemplateControl.SFRParams);
                            break;
                        case TemplateType.FOVParam:
                            TemplateDel(TemplateControl.FOVParams);
                            break;
                        case TemplateType.GhostParam:
                            TemplateDel(TemplateControl.GhostParams);
                            break;
                        case TemplateType.DistortionParam:
                            TemplateDel(TemplateControl.DistortionParams);
                            break;
                        case TemplateType.FlowParam:
                            TemplateDel(TemplateControl.FlowParams);
                            break;
                        case TemplateType.MeasureParm:
                            TemplateDel(TemplateControl.MeasureParams);
                            break;
                    }

                    ListConfigs.RemoveAt(ListView1.SelectedIndex);
                    ListView1.SelectedIndex = ListConfigs.Count - 1;
                    if (ListView1.SelectedIndex < 0)
                    {
                        if (UserControl is MeasureParamControl mpc){
                            mpc.ListConfigs.Clear();
                        }
                    }
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
