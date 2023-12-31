﻿using ColorVision.Extension;
using ColorVision.Flow.Templates;
using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using ColorVision.Services.Algorithm.Templates;
using ColorVision.Services.Device.PG.Templates;
using ColorVision.Util;
using Newtonsoft.Json;
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

        public ObservableCollection<TemplateModelBase> TemplateModelBases { get; set; } = new ObservableCollection<TemplateModelBase>();

        public WindowTemplate(TemplateType windowTemplateType, bool IsReLoad = true)
        {
            TemplateType = windowTemplateType;
            TemplateControl = TemplateControl.GetInstance();
            Load(windowTemplateType, IsReLoad);
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

        public void Load(TemplateType windowTemplateType,bool IsReLoad = true)
        {
            switch (TemplateType)
            {
                case TemplateType.FlowParam:
                    if (IsReLoad) 
                        TemplateControl.LoadParams(TemplateControl.FlowParams);
                    TemplateModelBases = TemplateControl.GetTemplateModelBases(TemplateControl.FlowParams);
                    Title = "流程引擎";
                    break;
                case TemplateType.MeasureParam:
                    if (IsReLoad) 
                        TemplateControl.LoadParams(TemplateControl.MeasureParams);
                    TemplateModelBases = TemplateControl.GetTemplateModelBases(TemplateControl.MeasureParams);
                    Title = "测量设置";
                    break;
                case TemplateType.Calibration:
                    if (IsReLoad) 
                        TemplateControl.LoadParams(TemplateControl.CalibrationParams);
                    TemplateModelBases = TemplateControl.GetTemplateModelBases(TemplateControl.CalibrationParams);
                    Title = "校正参数设置";
                    break;
                case TemplateType.LedResult:
                    if (IsReLoad) 
                        TemplateControl.LoadParams(TemplateControl.LedReusltParams);
                    TemplateModelBases = TemplateControl.GetTemplateModelBases(TemplateControl.LedReusltParams);
                    Title = "数据判断模板设置";
                    break;
                case TemplateType.AoiParam:
                    if (IsReLoad)
                        TemplateControl.LoadParams(TemplateControl.AoiParams);
                    TemplateModelBases = TemplateControl.GetTemplateModelBases(TemplateControl.AoiParams);
                    Title = "AOI参数设置";
                    break;
                case TemplateType.PGParam:
                    if (IsReLoad)
                        TemplateControl.LoadParams(TemplateControl.PGParams);
                    TemplateModelBases = TemplateControl.GetTemplateModelBases(TemplateControl.PGParams);
                    Title = "PG参数设置";
                    break;
                case TemplateType.SMUParam:
                    if (IsReLoad)
                        TemplateControl.LoadParams(TemplateControl.SMUParams);
                    TemplateModelBases = TemplateControl.GetTemplateModelBases(TemplateControl.SMUParams);
                    Title = "源表模板设置";
                    break;
                case TemplateType.PoiParam:
                    if (IsReLoad)
                        TemplateControl.LoadParams(TemplateControl.PoiParams);
                    TemplateModelBases = TemplateControl.GetTemplateModelBases(TemplateControl.PoiParams);
                    Title = "关注点设置";
                    break;
                case TemplateType.MTFParam:
                    if (IsReLoad)
                        TemplateControl.LoadParams(TemplateControl.MTFParams);
                    TemplateModelBases = TemplateControl.GetTemplateModelBases(TemplateControl.MTFParams);
                    Title = "MTF算法设置";
                    break;
                case TemplateType.SFRParam:
                    if (IsReLoad)
                        TemplateControl.LoadParams(TemplateControl.SFRParams);
                    TemplateModelBases = TemplateControl.GetTemplateModelBases(TemplateControl.SFRParams);
                    Title = "SFR算法设置";
                    break;
                case TemplateType.FOVParam:
                    if (IsReLoad)
                        TemplateControl.LoadParams(TemplateControl.FOVParams);
                    TemplateModelBases = TemplateControl.GetTemplateModelBases(TemplateControl.FOVParams);
                    Title = "FOV算法设置";
                    break;
                case TemplateType.GhostParam:
                    if (IsReLoad)
                        TemplateControl.LoadParams(TemplateControl.GhostParams);
                    TemplateModelBases = TemplateControl.GetTemplateModelBases(TemplateControl.GhostParams);
                    Title = "鬼影算法设置";
                    break;
                case TemplateType.DistortionParam:
                    if (IsReLoad)
                        TemplateControl.LoadParams(TemplateControl.DistortionParams);
                    TemplateModelBases = TemplateControl.GetTemplateModelBases(TemplateControl.DistortionParams);
                    Title = "畸变算法设置";
                    break;
                case TemplateType.LedCheckParam:
                    if (IsReLoad)
                        TemplateControl.LoadParams(TemplateControl.LedCheckParams);
                    TemplateModelBases = TemplateControl.GetTemplateModelBases(TemplateControl.LedCheckParams);
                    Title = "灯光检测算法设置";
                    break;
                case TemplateType.FocusPointsParam:
                    if (IsReLoad)
                        TemplateControl.LoadParams(TemplateControl.FocusPointsParams);
                    TemplateModelBases = TemplateControl.GetTemplateModelBases(TemplateControl.FocusPointsParams);
                    Title = "FocusPoints算法设置";
                    break;
                default:
                    break;
            }
        }


        public UserControl  UserControl { get; set; }
        public WindowTemplate(TemplateType windowTemplateType,UserControl userControl,bool IsReLoad = true)
        {
            TemplateType = windowTemplateType;
            TemplateControl = TemplateControl.GetInstance();
            Load(windowTemplateType, IsReLoad);
            InitializeComponent();
            GridProperty.Children.Clear();
            GridProperty.Margin = new Thickness(5,5,5,5);
            UserControl = userControl;
            GridProperty.Children.Add(UserControl);
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            ListView1.ItemsSource = TemplateModelBases;
            this.Closed += WindowTemplate_Closed;

            switch (TemplateType)
            {
                case TemplateType.PoiParam:
                    this.MinWidth = 300;

                    Width = 300;
                    break;
                case TemplateType.FlowParam:
                    this.MinWidth = 300;
                    Width = 300;
                    break;
            }
        }

        private void WindowTemplate_Closed(object? sender, EventArgs e)
        {
            TemplateSave();
        }

        private void ListView1_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                switch (TemplateType)
                {
                    case TemplateType.PoiParam:
                        if (TemplateModelBases[listView.SelectedIndex].GetValue() is PoiParam poiParam)
                        {
                            var WindowFocusPoint = new WindowFocusPoint(poiParam) { Owner = this };
                            WindowFocusPoint.Closed += async (s, e) =>
                            {
                                await Task.Delay(30);
                                TemplateModelBases[listView.SelectedIndex].Tag = $"{poiParam.Width}*{poiParam.Height}{(GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql ? "" : $"_{poiParam.PoiPoints.Count}")}";
                            };
                            WindowFocusPoint.Show();
                        }
                        break;
                    case TemplateType.FlowParam:
                        if (TemplateModelBases[listView.SelectedIndex].GetValue() is FlowParam flowParam)
                        {
                            flowParam.Name ??= TemplateModelBases[listView.SelectedIndex].Key;
                            new WindowFlowEngine(flowParam) { Owner =null }.Show();
                            this.Close();
                        }
                        break;
                }
            }
        }

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                switch (TemplateType)
                {
                    case TemplateType.Calibration:
                        if (UserControl is Calibration calibration && TemplateModelBases[listView.SelectedIndex].GetValue() is CalibrationParam calibrationParam)
                        {
                            calibration.DataContext = calibrationParam;
                            calibration.CalibrationParam = calibrationParam;
                        }
                        break;
                    case TemplateType.MeasureParam:
                        if (UserControl is MeasureParamControl mpc && TemplateModelBases[listView.SelectedIndex].GetValue() is MeasureParam mp)
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
                        PropertyGrid1.SelectedObject = TemplateModelBases[listView.SelectedIndex].GetValue();
                        break;
                }
            }
        }
        public string NewCreateFileName(string FileName)
        {
            List<string> Names = new List<string>();
            foreach (var item in TemplateModelBases)
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
            TemplateModelBases.Add(config);
            ListView1.SelectedIndex = TemplateModelBases.Count - 1;
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
                            TemplateDel(TemplateControl.CalibrationParams);
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

                    TemplateModelBases.RemoveAt(ListView1.SelectedIndex);
                    ListView1.SelectedIndex = TemplateModelBases.Count - 1;
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

        private void Button_Export_Click(object sender, RoutedEventArgs e)
        {
            if (ListView1.SelectedIndex < 0)
            {
                MessageBox.Show("请选择您要导出的流程", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                return;
            }


            switch (TemplateType)
            {
                case TemplateType.FlowParam:
                    if (true)
                    {
                        System.Windows.Forms.SaveFileDialog ofd = new System.Windows.Forms.SaveFileDialog();
                        ofd.DefaultExt = "stn";
                        ofd.Filter = "*.stn|*.stn";
                        ofd.AddExtension = false;
                        ofd.RestoreDirectory = true;
                        ofd.Title = "导出流程";
                        ofd.FileName = TemplateControl.FlowParams[ListView1.SelectedIndex].Key;
                        if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                        Tool.Base64ToFile(TemplateControl.FlowParams[ListView1.SelectedIndex].Value.DataBase64, ofd.FileName);
                    }

                    break;
                default:
                    if (true)
                    {
                        System.Windows.Forms.SaveFileDialog ofd = new System.Windows.Forms.SaveFileDialog();
                        ofd.DefaultExt = "cfg";
                        ofd.Filter = "*.cfg|*.cfg";
                        ofd.AddExtension = false;
                        ofd.RestoreDirectory = true;
                        ofd.Title = "导出流程";
                        ofd.FileName = TemplateModelBases[ListView1.SelectedIndex].Key;
                        if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                        if (TemplateModelBases[ListView1.SelectedIndex].GetValue() is ViewModelBase viewModelBase)
                        {
                            viewModelBase.ToJsonNFile(ofd.FileName);
                        }
                    }
                    break;
            }
        }

        private void Button_Import_Click(object sender, RoutedEventArgs e)
        {
            switch (TemplateType)
            {
                case TemplateType.FlowParam:
                    if (true)
                    {
                        System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
                        ofd.Filter = "*.stn|*.stn";
                        if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                        string name = Path.GetFileNameWithoutExtension(ofd.FileName);
                        FlowParam? flowParam = TemplateControl.AddFlowParam(name);
                        if (flowParam != null)
                        {
                            flowParam.FileName = Path.GetFileName(ofd.FileName); ;
                            CreateNewTemplate(TemplateControl.FlowParams, name, flowParam);

                            TemplateControl.GetInstance().Save2DB(flowParam);
                        }
                        else MessageBox.Show("数据库创建流程模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    }
                    break;
                case TemplateType.MeasureParam:
                    if (true)
                    {
                        System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
                        ofd.Filter = "*.cfg|*.cfg";
                        if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                        string name = Path.GetFileNameWithoutExtension(ofd.FileName);
                        MeasureParam? measureParam = JsonConvert.DeserializeObject<MeasureParam>(File.ReadAllText(ofd.FileName));         
                        if (measureParam != null)
                        {
                            CreateNewTemplate(TemplateControl.MeasureParams, name, measureParam);
                            TemplateControl.GetInstance().Save2DB(measureParam);
                        }
                        else MessageBox.Show("数据库创建流程模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    }
                    break;
                case TemplateType.Calibration:  
                    if (true)
                    {
                        System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
                        ofd.Filter = "*.cfg|*.cfg";
                        if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                        string name = Path.GetFileNameWithoutExtension(ofd.FileName);

                        CalibrationParam? calibrationParam1 = JsonConvert.DeserializeObject<CalibrationParam>(File.ReadAllText(ofd.FileName));
                        CalibrationParam? calibrationParam = TemplateControl.AddParamMode<CalibrationParam>(TemplateTypeFactory.GetModeTemplateType(TemplateType), name);
                        calibrationParam1.CopyTo(calibrationParam);
                        if (calibrationParam != null)
                        {
                            CreateNewTemplate(TemplateControl.CalibrationParams, name, calibrationParam);
                            TemplateControl.GetInstance().Save2DB(calibrationParam);
                        }
                        else MessageBox.Show("数据库创建流程模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    }
                    break;
                case TemplateType.LedResult:
                    if (true)
                    {
                        System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
                        ofd.Filter = "*.cfg|*.cfg";
                        if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                        string name = Path.GetFileNameWithoutExtension(ofd.FileName);
                        LedReusltParam? ledReusltParam = JsonConvert.DeserializeObject<LedReusltParam>(File.ReadAllText(ofd.FileName));
                        if (ledReusltParam != null)
                        {
                            CreateNewTemplate(TemplateControl.LedReusltParams, name, ledReusltParam);
                            TemplateControl.GetInstance().Save2DB(ledReusltParam);
                        }
                        else MessageBox.Show("数据库创建流程模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    }
                    break;
                case TemplateType.AoiParam:
                    if (true)
                    {
                        System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
                        ofd.Filter = "*.cfg|*.cfg";
                        if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                        string name = Path.GetFileNameWithoutExtension(ofd.FileName);
                        AOIParam? aoiParam = JsonConvert.DeserializeObject<AOIParam>(File.ReadAllText(ofd.FileName));
                        if (aoiParam != null)
                        {
                            CreateNewTemplate(TemplateControl.AoiParams, name, aoiParam);
                            TemplateControl.GetInstance().Save2DB(aoiParam);
                        }
                        else MessageBox.Show("数据库创建流程模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    }
                    break;
                case TemplateType.PGParam:
                    if (true)
                    {
                        System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
                        ofd.Filter = "*.cfg|*.cfg";
                        if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                        string name = Path.GetFileNameWithoutExtension(ofd.FileName);
                        PGParam? pGParam = JsonConvert.DeserializeObject<PGParam>(File.ReadAllText(ofd.FileName));
                        if (pGParam != null)
                        {
                            CreateNewTemplate(TemplateControl.PGParams, name, pGParam);
                            TemplateControl.GetInstance().Save2DB(pGParam);
                        }
                        else MessageBox.Show("数据库创建流程模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    }
                    break;
                case TemplateType.SMUParam:
                    if (true)
                    {
                        System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
                        ofd.Filter = "*.cfg|*.cfg";
                        if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                        string name = Path.GetFileNameWithoutExtension(ofd.FileName);
                        SMUParam? sMUParam = JsonConvert.DeserializeObject<SMUParam>(File.ReadAllText(ofd.FileName));
                        if (sMUParam != null)
                        {
                            CreateNewTemplate(TemplateControl.SMUParams, name, sMUParam);
                            TemplateControl.GetInstance().Save2DB(sMUParam);
                        }
                        else MessageBox.Show("数据库创建源表模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    }
                    break;  
                case TemplateType.PoiParam:
                    if (true)
                    {
                        System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
                        ofd.Filter = "*.cfg|*.cfg";
                        if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                        string name = Path.GetFileNameWithoutExtension(ofd.FileName);
                        PoiParam? poiParam = JsonConvert.DeserializeObject<PoiParam>(File.ReadAllText(ofd.FileName));
                        if (poiParam != null)
                        {
                            CreateNewTemplate(TemplateControl.PoiParams, name, poiParam);
                            TemplateControl.GetInstance().Save2DB(poiParam);
                        }
                        else MessageBox.Show("数据库创建POI模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    }
                    break;
                case TemplateType.MTFParam:
                    if (true)
                    {
                        System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
                        ofd.Filter = "*.cfg|*.cfg";
                        if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                        string name = Path.GetFileNameWithoutExtension(ofd.FileName);
                        MTFParam? mTFParam = JsonConvert.DeserializeObject<MTFParam>(File.ReadAllText(ofd.FileName));
                        if (mTFParam != null)
                        {
                            CreateNewTemplate(TemplateControl.MTFParams, name, mTFParam);
                            TemplateControl.GetInstance().Save2DB(mTFParam);
                        }
                        else MessageBox.Show("数据库创建MTF模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    }
                    break;
                case TemplateType.SFRParam:
                    if (true)
                    {
                        System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
                        ofd.Filter = "*.cfg|*.cfg";
                        if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                        string name = Path.GetFileNameWithoutExtension(ofd.FileName);
                        SFRParam? sFRParam = JsonConvert.DeserializeObject<SFRParam>(File.ReadAllText(ofd.FileName));
                        if (sFRParam != null)
                        {
                            CreateNewTemplate(TemplateControl.SFRParams, name, sFRParam);
                            TemplateControl.GetInstance().Save2DB(sFRParam);
                        }
                        else MessageBox.Show("数据库创建SFR模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    }
                    break;
                case TemplateType.FOVParam:
                    if (true)
                    {
                        System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
                        ofd.Filter = "*.cfg|*.cfg";
                        if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                        FOVParam? fOVParam = JsonConvert.DeserializeObject<FOVParam>(File.ReadAllText(ofd.FileName));
                        string name = Path.GetFileNameWithoutExtension(ofd.FileName);
                        if (fOVParam != null)
                        {
                            CreateNewTemplate(TemplateControl.FOVParams, name, fOVParam);
                            TemplateControl.GetInstance().Save2DB(fOVParam);
                        }
                        else MessageBox.Show("数据库创建FOV模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    }
                    break;
                case TemplateType.GhostParam:
                    if (true)
                    {
                        System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
                        ofd.Filter = "*.cfg|*.cfg";
                        GhostParam? ghostParam = JsonConvert.DeserializeObject<GhostParam>(File.ReadAllText(ofd.FileName));
                        if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                        string name = Path.GetFileNameWithoutExtension(ofd.FileName);
                        if (ghostParam != null)
                        {
                            CreateNewTemplate(TemplateControl.GhostParams, name, ghostParam);
                            TemplateControl.GetInstance().Save2DB(ghostParam);
                        }
                        else MessageBox.Show("数据库创建Ghost模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    }
                    break;
                case TemplateType.DistortionParam:
                    if (true)
                    {
                        System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
                        ofd.Filter = "*.cfg|*.cfg";

                        if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                        string name = Path.GetFileNameWithoutExtension(ofd.FileName);
                        DistortionParam? distortionParam = JsonConvert.DeserializeObject<DistortionParam>(File.ReadAllText(ofd.FileName));
                        if (distortionParam != null)
                        {
                            CreateNewTemplate(TemplateControl.DistortionParams, name, distortionParam);
                            TemplateControl.GetInstance().Save2DB(distortionParam);
                        }
                        else MessageBox.Show("数据库创建Distortion模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    }
                    break;
                case TemplateType.LedCheckParam:
                    if (true)
                    {
                        System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
                        ofd.Filter = "*.cfg|*.cfg";

                        if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                        string name = Path.GetFileNameWithoutExtension(ofd.FileName);
                        LedCheckParam? ledCheckParam = JsonConvert.DeserializeObject<LedCheckParam>(File.ReadAllText(ofd.FileName));
                        if (ledCheckParam != null)
                        {
                            CreateNewTemplate(TemplateControl.LedCheckParams, name, ledCheckParam);
                            TemplateControl.GetInstance().Save2DB(ledCheckParam);
                        }
                        else MessageBox.Show("数据库创建灯光检测模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    }
                    break;
                case TemplateType.FocusPointsParam:
                    if (true)
                    {
                        System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
                        ofd.Filter = "*.cfg|*.cfg";

                        if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                        string name = Path.GetFileNameWithoutExtension(ofd.FileName);
                        FocusPointsParam? focusPointsParam = JsonConvert.DeserializeObject<FocusPointsParam>(File.ReadAllText(ofd.FileName));
                        if (focusPointsParam != null)
                        {
                            CreateNewTemplate(TemplateControl.FocusPointsParams, name, focusPointsParam);
                            TemplateControl.GetInstance().Save2DB(focusPointsParam);
                        }
                        else MessageBox.Show("数据库创建FocusPoints模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    }
                    break;
                default:
                    break;
            }



        }
    }
}
