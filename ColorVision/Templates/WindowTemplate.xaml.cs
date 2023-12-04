using ColorVision.Extension;
using ColorVision.Flow.Templates;
using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using ColorVision.Services.Algorithm.Templates;
using ColorVision.Services.Device.PG.Templates;
using ColorVision.Util;
using Org.BouncyCastle.Bcpg.OpenPgp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Templates
{
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
            GridProperty.Margin = new Thickness(5,5,5,5);
            UserControl = userControl;
            GridProperty.Children.Add(UserControl);
        }


        public new void ShowDialog()
        {
            switch (TemplateType)
            {
                case TemplateType.PoiParam:
                    this.MinWidth = 390;
                    this.Width = 390;
                    this.Closed += (s, e) =>
                    {
                        TemplateControl.Save(TemplateType);
                    };
                    break;
                case TemplateType.FlowParam:
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
                        else MessageBox.Show("数据库创建流程模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    };
                    FunctionGrid.Children.Insert(2, button);

                    Button button1 = new Button() { Content = "导出流程", Width = 80 };
                    button1.Click += (s, e) =>
                    {
                        if (ListView1.SelectedIndex<0)
                        {
                            MessageBox.Show("请选择您要导出的流程", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
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
                        Tool.Base64ToFile(TemplateControl.FlowParams[ListView1.SelectedIndex].Value.DataBase64, ofd.FileName);
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

        public ObservableCollection<TemplateModelBase> ListConfigs { get; set; } = new ObservableCollection<TemplateModelBase>();
        private void Window_Initialized(object sender, EventArgs e)
        {
            ListView1.ItemsSource = ListConfigs;
        }


        private void ListView1_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                switch (TemplateType)
                {
                    case TemplateType.PoiParam:
                        if (ListConfigs[listView.SelectedIndex].GetValue() is PoiParam poiParam)
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
                        if (ListConfigs[listView.SelectedIndex].GetValue() is FlowParam flowParam)
                        {
                            flowParam.Name ??= ListConfigs[listView.SelectedIndex].Key;
                            new WindowFlowEngine(flowParam) { Owner =null }.Show();
                            this.Close();
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
                        if (UserControl is Calibration calibration && ListConfigs[listView.SelectedIndex].GetValue() is CalibrationParam calibrationParam)
                        {
                            calibration.DataContext = calibrationParam;
                            calibration.CalibrationParam = calibrationParam;
                        }
                        break;
                    case TemplateType.MeasureParam:
                        if (UserControl is MeasureParamControl mpc && ListConfigs[listView.SelectedIndex].GetValue() is MeasureParam mp)
                        {
                            mpc.MasterID = mp.ID;
                            List<MeasureDetailModel> des = TemplateControl.LoadMeasureDetail(mp.ID);
                            mpc.Reload(des);
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
                        PropertyGrid1.SelectedObject = ListConfigs[listView.SelectedIndex].GetValue();
                        break;
                }
            }
        }
        public string NewCreateFileName(string FileName)
        {
            List<string> Names = new List<string>();
            foreach (var item in ListConfigs)
            {
                Names.Add(item.Key);
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
                    CalibrationParam? CalibrationParam = TemplateControl.AddParamMode<CalibrationParam>(TemplateTypeFactory.GetModeTemplateType(TemplateType), TextBox1.Text);
                    if (CalibrationParam != null) CreateNewTemplate(TemplateControl.CalibrationParams, TextBox1.Text, CalibrationParam);
                    else MessageBox.Show("数据库创建CalibrationParam模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    break;
                case TemplateType.LedResult:
                    CreateNewTemplate(TemplateControl.LedReusltParams, TextBox1.Text, new LedReusltParam());
                    break;
                case TemplateType.AoiParam:
                    AOIParam? aoiParam = TemplateControl.AddParamMode<AOIParam>(TemplateTypeFactory.GetModeTemplateType(TemplateType), TextBox1.Text);
                    if (aoiParam != null) CreateNewTemplate(TemplateControl.AoiParams, TextBox1.Text, aoiParam);
                    else MessageBox.Show("数据库创建AOI模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    break;
                case TemplateType.PGParam:
                    PGParam? pgParam = TemplateControl.AddParamMode<PGParam>(TemplateTypeFactory.GetModeTemplateType(TemplateType), TextBox1.Text);
                    if (pgParam != null) CreateNewTemplate(TemplateControl.PGParams, TextBox1.Text, pgParam);
                    else MessageBox.Show("数据库创建PG模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    break;

                case TemplateType.SMUParam:
                    SMUParam?  sMUParam = TemplateControl.AddParamMode<SMUParam>(TemplateTypeFactory.GetModeTemplateType(TemplateType), TextBox1.Text);
                    if (sMUParam != null) CreateNewTemplate(TemplateControl.SMUParams, TextBox1.Text, sMUParam);
                    else MessageBox.Show("数据库创建源表模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    break;
                case TemplateType.MTFParam:
                    MTFParam? mTFParam = TemplateControl.AddParamMode<MTFParam>(TemplateTypeFactory.GetModeTemplateType(TemplateType), TextBox1.Text);
                    if (mTFParam != null) CreateNewTemplate(TemplateControl.MTFParams, TextBox1.Text, mTFParam);
                    else MessageBox.Show("数据库创建MTF模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    break;
                case TemplateType.SFRParam:
                    SFRParam? sFRParam = TemplateControl.AddParamMode<SFRParam>(TemplateTypeFactory.GetModeTemplateType(TemplateType), TextBox1.Text);
                    if (sFRParam != null) CreateNewTemplate(TemplateControl.SFRParams, TextBox1.Text, sFRParam);
                    else MessageBox.Show("数据库创建MTF模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly); break;
                case TemplateType.FOVParam:
                    FOVParam? fOVParam = TemplateControl.AddParamMode<FOVParam>(TemplateTypeFactory.GetModeTemplateType(TemplateType), TextBox1.Text);
                    if (fOVParam != null) CreateNewTemplate(TemplateControl.FOVParams, TextBox1.Text, fOVParam);
                    else MessageBox.Show("数据库创建FOV模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly); break;
                case TemplateType.GhostParam:
                    GhostParam? ghostParam = TemplateControl.AddParamMode<GhostParam>(TemplateTypeFactory.GetModeTemplateType(TemplateType), TextBox1.Text);
                    if (ghostParam != null) CreateNewTemplate(TemplateControl.GhostParams, TextBox1.Text, ghostParam);
                    else MessageBox.Show("数据库创建Ghost模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly); break;
                case TemplateType.DistortionParam:
                    DistortionParam? distortionParam = TemplateControl.AddParamMode<DistortionParam>(TemplateTypeFactory.GetModeTemplateType(TemplateType), TextBox1.Text);
                    if (distortionParam != null) CreateNewTemplate(TemplateControl.DistortionParams, TextBox1.Text, distortionParam);
                    else MessageBox.Show("数据库创建Distortion模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly); break;
                case TemplateType.FocusPointsParam:
                    FocusPointsParam? focusPointsParam = TemplateControl.AddParamMode<FocusPointsParam>(TemplateTypeFactory.GetModeTemplateType(TemplateType),TextBox1.Text);
                    if (focusPointsParam != null) CreateNewTemplate(TemplateControl.FocusPointsParams, TextBox1.Text, focusPointsParam);
                    else MessageBox.Show("数据库创建FocusPoints模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    break;
                case TemplateType.LedCheckParam:
                    LedCheckParam? ledCheckParam = TemplateControl.AddParamMode<LedCheckParam>(TemplateTypeFactory.GetModeTemplateType(TemplateType), TextBox1.Text);
                    if (ledCheckParam != null) CreateNewTemplate(TemplateControl.LedCheckParams, TextBox1.Text, ledCheckParam);
                    else MessageBox.Show("数据库创建灯光检测模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    break;
                case TemplateType.PoiParam:
                    PoiParam? poiParam = TemplateControl.AddPoiParam(TextBox1.Text);
                    if (poiParam != null) CreateNewTemplate(TemplateControl.PoiParams, TextBox1.Text, poiParam);
                    else MessageBox.Show("数据库创建POI模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    break;
                case TemplateType.FlowParam:
                    FlowParam? flowParam = TemplateControl.AddFlowParam(TextBox1.Text);
                    if (flowParam != null) CreateNewTemplate(TemplateControl.FlowParams, TextBox1.Text, flowParam);
                    else MessageBox.Show("数据库创建流程模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    break;
                case TemplateType.MeasureParam:
                    MeasureParam? measureParam = TemplateControl.AddMeasureParam(TextBox1.Text);
                    if (measureParam != null) CreateNewTemplate(TemplateControl.MeasureParams, TextBox1.Text, measureParam);
                    else MessageBox.Show("数据库创建流程模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    break;

            }
        }

        private void CreateNewTemplateFromCsv()
        {
            switch (TemplateType)
            {
                case TemplateType.AoiParam:
                    CreateNewTemplate(TemplateControl.AoiParams, TextBox1.Text, new AOIParam());
                    break;
                case TemplateType.Calibration:
                    CreateNewTemplate(TemplateControl.CalibrationParams, TextBox1.Text, new CalibrationParam());
                    break;
                case TemplateType.PGParam:
                    CreateNewTemplate(TemplateControl.PGParams, TextBox1.Text, new PGParam());
                    break;
                case TemplateType.LedResult:
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
                case TemplateType.FocusPointsParam:
                    CreateNewTemplate(TemplateControl.FocusPointsParams, TextBox1.Text, new FocusPointsParam() { });
                    break;
                case TemplateType.LedCheckParam:
                    CreateNewTemplate(TemplateControl.LedCheckParams, TextBox1.Text, new LedCheckParam() { });
                    break;
                case TemplateType.PoiParam:
                    CreateNewTemplate(TemplateControl.PoiParams, TextBox1.Text , new PoiParam() { });
                    break;
                case TemplateType.FlowParam:
                    CreateNewTemplate(TemplateControl.FlowParams, TextBox1.Text, new FlowParam() { Name = TextBox1.Text });
                    break;
            }
        }



        private void Button_Save_Click(object sender, RoutedEventArgs e)
        {
            TemplateSave();
        }

        private void Button_New_Click(object sender, RoutedEventArgs e)
        {
            TemplateNew();
        }


        private void Button_Del_Click(object sender, RoutedEventArgs e)
        {
            TemplateDel();
        }
        public void TemplateSave()
        {
            TemplateControl.Save(TemplateType);
            this.Close();
        }


        public void TemplateNew()
        {
            if (!string.IsNullOrEmpty(TextBox1.Text))
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

        private void CreateNewTemplate<T>(ObservableCollection<TemplateModel<T>> keyValuePairs, string Name, T t) where T : ParamBase
        {
            keyValuePairs.Add(new TemplateModel<T>(Name, t));
            TemplateModel<T> config = new TemplateModel<T> {Value = t, Key = Name, };
            ListConfigs.Add(config);
            ListView1.SelectedIndex = ListConfigs.Count - 1;
            ListView1.ScrollIntoView(config);
        }

        public void TemplateDel()
        {
            void TemplateDel<T>(ObservableCollection<TemplateModel<T>> keyValuePairs) where T : ParamBase
            {
                if (GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql)
                    TemplateControl.ModMasterDeleteById(keyValuePairs[ListView1.SelectedIndex].Value.ID);
                keyValuePairs.RemoveAt(ListView1.SelectedIndex);
            }

            if (ListView1.SelectedIndex > -1)
            {
                if (MessageBox.Show($"是否删除模板{ListView1.SelectedIndex + 1},删除后无法恢复!", Application.Current.MainWindow.Title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK)
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
                        case TemplateType.LedResult:
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
                        case TemplateType.FocusPointsParam:
                            TemplateDel(TemplateControl.FocusPointsParams);
                            break;
                        case TemplateType.LedCheckParam:
                            TemplateDel(TemplateControl.LedCheckParams);
                            break;
                        case TemplateType.FlowParam:
                            TemplateDel(TemplateControl.FlowParams);
                            break;
                        case TemplateType.MeasureParam:
                            TemplateDel(TemplateControl.MeasureParams);
                            break;
                    }

                    ListConfigs.RemoveAt(ListView1.SelectedIndex);
                    ListView1.SelectedIndex = ListConfigs.Count - 1;
                    if (ListView1.SelectedIndex < 0)
                    {
                        if (UserControl is MeasureParamControl mpc)
                        {
                            mpc.ListConfigs.Clear();
                        }
                    }
                }
            }
            else
            {
                MessageBox.Show("请先选择", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
            }
        }



        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
        }


        private void ListView1_Loaded(object sender, RoutedEventArgs e)
        {
            TextBox1.Text = NewCreateFileName("default");
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Tag is TemplateModelBase templateModelBase)
            {
                templateModelBase.IsEditMode = false;
            }
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {

        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Tag is TemplateModelBase templateModelBase)
            {
                if (e.Key == Key.F2)
                {
                    templateModelBase.IsEditMode = true;
                }
                if (e.Key == Key.Escape || e.Key == Key.Enter)
                {
                    templateModelBase.IsEditMode = false;
                }
            }
        }

        private void TextBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Tag is TemplateModelBase templateModelBase)
            {
                templateModelBase.IsEditMode = true;
            }
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is TemplateModelBase templateModelBase)
            {
                templateModelBase.IsEditMode = true;
            }
        }
    }
}
