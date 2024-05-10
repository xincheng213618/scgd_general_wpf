using ColorVision.Common.MVVM;
using ColorVision.Common.Sorts;
using ColorVision.Common.Utilities;
using ColorVision.Extension;
using ColorVision.Properties;
using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Services.Dao.Validate;
using ColorVision.Services.Devices.Algorithm.Templates;
using ColorVision.Services.Devices.PG.Templates;
using ColorVision.Services.Devices.Sensor.Templates;
using ColorVision.Services.Devices.SMU;
using ColorVision.Services.Devices.Spectrum;
using ColorVision.Services.Flow;
using ColorVision.Services.PhyCameras.Templates;
using ColorVision.Services.Templates.Measure;
using ColorVision.Services.Templates.POI;
using ColorVision.Services.Templates.POI.Dao;
using ColorVision.Settings;
using Newtonsoft.Json;
using NPOI.SS.Formula.Functions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Services.Templates
{


    /// <summary>
    /// CalibrationTemplate.xaml 的交互逻辑
    /// </summary>
    public partial class WindowTemplate : Window 
    {
        TemplateType TemplateType { get; set; }
        TemplateControl TemplateControl { get;set; }
        public ITemplate ITemplate { get; set; }

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
                        FlowParam.LoadFlowParam();
                    ITemplate = new ITemplate<FlowParam>() { TemplateParams = FlowParam.Params ,Title = "流程引擎" };
                    break;
                case TemplateType.MeasureParam:
                    if (IsReLoad)
                        MeasureParam.LoadMeasureParams();
                    ITemplate = new ITemplate<MeasureParam>() { TemplateParams = MeasureParam.MeasureParams, Title = "测量设置" };
                    break;
                case TemplateType.AoiParam:
                    if (IsReLoad)
                        TemplateControl.LoadModParam(TemplateControl.AoiParams, ModMasterType.Aoi);
                    ITemplate = new ITemplate<AOIParam>() { TemplateParams = TemplateControl.AoiParams ,Title = "AOI参数设置" };
                    break;
                case TemplateType.PGParam:
                    if (IsReLoad)
                        TemplateControl.LoadModParam(PGParam.Params, ModMasterType.PG);
                    ITemplate = new ITemplate<PGParam>() { TemplateParams = PGParam.Params ,Title = "PG参数设置" };
                    break;
                case TemplateType.SMUParam:
                    if (IsReLoad)
                        TemplateControl.LoadModParam(SMUParam.Params, ModMasterType.SMU);
                    ITemplate = new ITemplate<SMUParam>() { TemplateParams = SMUParam.Params , Title = "源表模板设置" };
                    break;
                case TemplateType.PoiParam:
                    if (IsReLoad)
                        PoiParam.LoadPoiParam();
                    ITemplate = new ITemplate<PoiParam>() { TemplateParams = PoiParam.Params , Title = "关注点设置"};
                    break;
                case TemplateType.MTFParam:
                    if (IsReLoad)
                        TemplateControl.LoadModParam(MTFParam.MTFParams, ModMasterType.MTF);
                    ITemplate = new ITemplate<MTFParam>() { TemplateParams = MTFParam.MTFParams  , Title= "MTF算法设置" };
                    break;
                case TemplateType.SFRParam:
                    if (IsReLoad)
                        TemplateControl.LoadModParam(SFRParam.SFRParams, ModMasterType.SFR);
                    ITemplate = new ITemplate<SFRParam>() { TemplateParams = SFRParam.SFRParams , Title = "SFR算法设置"};
                    break;
                case TemplateType.FOVParam:
                    if (IsReLoad)
                        TemplateControl.LoadModParam(FOVParam.FOVParams, ModMasterType.FOV);
                    ITemplate = new ITemplate<FOVParam>() { TemplateParams = FOVParam.FOVParams , Title = "FOV算法设置" };
                    break;
                case TemplateType.GhostParam:
                    if (IsReLoad)
                        TemplateControl.LoadModParam(GhostParam.GhostParams, ModMasterType.Ghost);
                    ITemplate = new ITemplate<GhostParam>() { TemplateParams = GhostParam.GhostParams, Title = "鬼影算法设置" };
                    break;
                case TemplateType.DistortionParam:
                    if (IsReLoad)
                        TemplateControl.LoadModParam(DistortionParam.DistortionParams, ModMasterType.Distortion);
                    ITemplate = new ITemplate<DistortionParam>() { TemplateParams = DistortionParam.DistortionParams , Title = "畸变算法设置" };
                    break;
                case TemplateType.LedCheckParam:
                    if (IsReLoad)
                        TemplateControl.LoadModParam(LedCheckParam.LedCheckParams, ModMasterType.LedCheck);
                    ITemplate = new ITemplate<LedCheckParam>() { TemplateParams = LedCheckParam.LedCheckParams , Title = "灯光检测算法设置" };
                    break;
                case TemplateType.FocusPointsParam:
                    if (IsReLoad)
                        TemplateControl.LoadModParam(FocusPointsParam.FocusPointsParams, ModMasterType.FocusPoints);
                    ITemplate = new ITemplate<FocusPointsParam>() { TemplateParams = FocusPointsParam.FocusPointsParams , Title = "FocusPoints算法设置" };
                    break;
                case TemplateType.BuildPOIParmam:
                    if (IsReLoad)
                        TemplateControl.LoadModParam(BuildPOIParam.BuildPOIParams, ModMasterType.BuildPOI);
                    ITemplate = new ITemplate<BuildPOIParam>() { TemplateParams = BuildPOIParam.BuildPOIParams , Title = "BuildPOI算法设置" };
                    break;
                case TemplateType.SensorHeYuan:
                    if (IsReLoad)
                        TemplateControl.LoadModParam(SensorHeYuan.SensorHeYuans, ModMasterType.SensorHeYuan);
                    ITemplate = new ITemplate<SensorHeYuan>() { TemplateParams = SensorHeYuan.SensorHeYuans , Title = "SensorHeYuan算法设置" };
                    break;
                case TemplateType.CameraExposureParam:
                    if (IsReLoad)
                        TemplateControl.LoadModParam(CameraExposureParam.CameraExposureParams, ModMasterType.CameraExposure);
                    ITemplate = new ITemplate<CameraExposureParam>() { TemplateParams = CameraExposureParam.CameraExposureParams , Title = "相机曝光参数设置" };
                    break;
                case TemplateType.ValidateParam:
                    ITemplate = new ITemplate<ValidateParam>() { TemplateParams = ValidateParam.Params , Title = "校验参数设置" };
                    break;
                default:
                    break;
            }
        }

        public UserControl  UserControl { get; set; }

        public ICalibrationService<BaseResourceObject> DeviceCamera { get; set; }

        public WindowTemplate(TemplateType windowTemplateType, UserControl userControl, ICalibrationService<BaseResourceObject> deviceCamera ,bool IsReLoad = true)
        {
            DeviceCamera = deviceCamera;
            TemplateType = windowTemplateType;
            TemplateControl = TemplateControl.GetInstance();
            UserControl = userControl;
            if (IsReLoad)
            {
                CalibrationParam.LoadResourceParams(DeviceCamera.CalibrationParams, DeviceCamera.SysResourceModel.Id, ModMasterType.Calibration);
            }
            ITemplate = new ITemplate<CalibrationParam>() { TemplateParams = DeviceCamera.CalibrationParams , Title = "校正参数设置" };

            InitializeComponent();
            GridProperty.Children.Clear();
            GridProperty.Margin = new Thickness(5, 5, 5, 5);
            GridProperty.Children.Add(UserControl);
            Width = Width + 200;
            ListView1.ItemsSource = DeviceCamera.CalibrationParams;


        }

        public DeviceSpectrum DeviceSpectrum { get; set; }
        public WindowTemplate(TemplateType windowTemplateType, UserControl userControl, DeviceSpectrum deviceSpectrum, bool IsReLoad = true)
        {
            DeviceSpectrum = deviceSpectrum;
            TemplateType = windowTemplateType;
            TemplateControl = TemplateControl.GetInstance();
            UserControl = userControl;
            if (IsReLoad)
            {
                CalibrationParam.LoadResourceParams(DeviceSpectrum.SpectrumResourceParams, DeviceSpectrum.SysResourceModel.Id, ModMasterType.SpectrumResource);
            }
            ITemplate = new ITemplate<SpectrumResourceParam>() { TemplateParams = DeviceSpectrum.SpectrumResourceParams , Title = "SpectrumResourceParams" };
            InitializeComponent();
            GridProperty.Children.Clear();
            GridProperty.Margin = new Thickness(5, 5, 5, 5);
            GridProperty.Children.Add(UserControl);
        }


        private void Window_Initialized(object sender, EventArgs e)
        {
            Title = ITemplate.Title;
            ListView1.ItemsSource = ITemplate.ItemsSource;
            ListView1.SelectedIndex = 0;
            if (ListView1.View is GridView gridView1)
                GridViewColumnVisibility.AddGridViewColumn(gridView1.Columns, GridViewColumnVisibilitys);
            Closed += WindowTemplate_Closed;

            switch (TemplateType)
            {
                case TemplateType.PoiParam:
                    MinWidth = 350;
                    Width = 350;
                    break;
                case TemplateType.FlowParam:
                    MinWidth = 350;
                    Width = 350;
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
                        var WindowFocusPoint = new WindowFocusPoint(PoiParam.Params[listView.SelectedIndex].Value) { Owner = this };
                        WindowFocusPoint.Show();
                        break;
                    case TemplateType.FlowParam:
                        if (FlowParam.Params[listView.SelectedIndex].Value is FlowParam flowParam)
                        {
                            flowParam.Name ??= FlowParam.Params[listView.SelectedIndex].Key;
                            new WindowFlowEngine(flowParam) { Owner =null }.Show();
                            Close();
                        }
                        break;
                }
            }
        }
        private MeasureMasterDao measureMaster = new MeasureMasterDao();
        private MeasureDetailDao measureDetail = new MeasureDetailDao();

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                switch (TemplateType)
                {
                    case TemplateType.Calibration:
                        if (UserControl is CalibrationControl calibration)
                        {
                            calibration.Initializedsss(DeviceCamera, DeviceCamera.CalibrationParams[listView.SelectedIndex].Value);
                        }
                        break;
                    case TemplateType.SpectrumResourceParam:
                        if (UserControl is SpectrumResourceControl spectrumResourceControl)
                        {
                            spectrumResourceControl.Initializedsss(DeviceSpectrum, DeviceSpectrum.SpectrumResourceParams[listView.SelectedIndex].Value);
                        }
                        break;
                    case TemplateType.MeasureParam:
                        if (UserControl is MeasureParamControl mpc && MeasureParam.MeasureParams[listView.SelectedIndex].Value is MeasureParam mp)
                        {
                            mpc.MasterID = mp.Id;
                            List<MeasureDetailModel> des = measureDetail.GetAllByPid(mp.Id); 
                            mpc.Reload(des);
                            mpc.ModTypeConfigs.Clear();
                            mpc.ModTypeConfigs.Add(new MParamConfig(-1,"关注点","POI"));
                            List<SysModMasterModel> sysModMaster = SysModMasterDao.Instance.GetAllById(ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);
                            foreach (SysModMasterModel model in sysModMaster)
                            {
                                mpc.ModTypeConfigs.Add(new MParamConfig(model));
                            }
                        }
                        break;
                    default:
                        PropertyGrid1.SelectedObject = ITemplate.GetValue(listView.SelectedIndex);
                        break;
                }
            }
        }

        private void CreateNewTemplateFromDB(string TemplateName)
        {
            switch (TemplateType)
            {
                case TemplateType.Calibration:
                    ITemplate.Create(TemplateName, DeviceCamera.SysResourceModel.Id);
                    break;
                case TemplateType.SpectrumResourceParam:
                    ITemplate.Create(TemplateName, DeviceSpectrum.SysResourceModel.Id);
                    break;
                case TemplateType.PoiParam:
                    PoiParam? poiParam = PoiParam.AddPoiParam(TemplateName);
                    if (poiParam != null) CreateNewTemplate(PoiParam.Params, TemplateName, poiParam);
                    else MessageBox.Show("数据库创建POI模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    break;
                case TemplateType.FlowParam:
                    FlowParam? flowParam = FlowParam.AddFlowParam(TemplateName);
                    if (flowParam != null) CreateNewTemplate(FlowParam.Params, TemplateName, flowParam);
                    else MessageBox.Show("数据库创建流程模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    break;
                case TemplateType.MeasureParam:
                    MeasureParam? measureParam = MeasureParam.AddMeasureParam(TemplateName);
                    if (measureParam != null) CreateNewTemplate(MeasureParam.MeasureParams, TemplateName, measureParam);
                    else MessageBox.Show("数据库创建测量模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    break;
                default:
                    ITemplate.Create(TemplateName);
                    break;
            }
        }

        private void Button_Save_Click(object sender, RoutedEventArgs e)
        {
            Close();
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
            switch (TemplateType)
            {
                case TemplateType.PoiParam:
                    foreach (var item in PoiParam.Params)
                    {
                        var modMasterModel = PoiMasterDao.Instance.GetById(item.Id);
                        if (modMasterModel != null)
                        {
                            modMasterModel.Name = item.Key;
                            PoiMasterDao.Instance.Save(modMasterModel);
                        }
                    }
                    break;
                case TemplateType.FlowParam:
                    FlowParam.Save2DB(FlowParam.Params);
                    break;
                default:
                    ITemplate.Save();
                    break;
            }

        }


        public void TemplateNew()
        {
            CreateTemplate createWindow = new CreateTemplate(ITemplate) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            createWindow.Closed += (s, e) =>
            {
                if (!string.IsNullOrEmpty(createWindow.CreateName))
                {
                    CreateNewTemplateFromDB(createWindow.CreateName);
                }
                else
                {
                    MessageBox.Show("请输入模板名称", Application.Current.MainWindow.Title, MessageBoxButton.OK);
                }
            };
            createWindow.Show();



        }

        private void CreateNewTemplate<T>(ObservableCollection<TemplateModel<T>> keyValuePairs, string Name, T t) where T : ParamBase
        {
            var a = new TemplateModel<T>(Name, t);
            keyValuePairs.Add(a);
            ListView1.SelectedIndex = keyValuePairs.Count - 1;
            ListView1.ScrollIntoView(a);
        }

        public void TemplateDel()
        {
            if (ListView1.SelectedIndex > -1)
            {
                if (MessageBox.Show(Application.Current.GetActiveWindow() ,$"是否删除模板{ListView1.SelectedIndex + 1},删除后无法恢复!", Application.Current.MainWindow.Title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK)
                {
                    switch (TemplateType)
                    {
                        case TemplateType.PoiParam:
                            PoiMasterDao poiMasterDao = new PoiMasterDao();
                            poiMasterDao.DeleteById(PoiParam.Params[ListView1.SelectedIndex].Value.Id);
                            PoiParam.Params.RemoveAt(ListView1.SelectedIndex);
                            break;
                        default:
                            ITemplate.Delete(ListView1.SelectedIndex);
                            break;
                    }

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
                MessageBox.Show(Application.Current.GetActiveWindow(), "请先选择", "ColorVision");
            }
        }



        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
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
                        ofd.FileName = FlowParam.Params[ListView1.SelectedIndex].Key;
                        if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                        Tool.Base64ToFile(FlowParam.Params[ListView1.SelectedIndex].Value.DataBase64, ofd.FileName);
                    }

                    break;
                default:
                    if (true)
                    {
                        //System.Windows.Forms.SaveFileDialog ofd = new System.Windows.Forms.SaveFileDialog();
                        //ofd.DefaultExt = "cfg";
                        //ofd.Filter = "*.cfg|*.cfg";
                        //ofd.AddExtension = false;
                        //ofd.RestoreDirectory = true;
                        //ofd.Title = "导出流程";
                        //ofd.FileName = TemplateParams[ListView1.SelectedIndex].Key;
                        //if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                        //if (TemplateParams[ListView1.SelectedIndex].GetValue() is ViewModelBase viewModelBase)
                        //{
                        //    viewModelBase.ToJsonNFile(ofd.FileName);
                        //}
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
                        FlowParam? flowParam = FlowParam.AddFlowParam(name);
                        if (flowParam != null)
                        {
                            flowParam.DataBase64 = Tool.FileToBase64(ofd.FileName); ;
                            CreateNewTemplate(FlowParam.Params, name, flowParam);

                            TemplateControl.Save2DB(flowParam);
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
                            CreateNewTemplate(MeasureParam.MeasureParams, name, measureParam);
                            TemplateControl.Save2DB(measureParam);
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
                        CalibrationParam? calibrationParam = TemplateControl.AddParamMode<CalibrationParam>(ModMasterType.Calibration, name);
                        if (calibrationParam1 != null && calibrationParam!=null)
                            calibrationParam1.CopyTo(calibrationParam);
                        if (calibrationParam != null)
                        {
                            CreateNewTemplate(DeviceCamera.CalibrationParams, name, calibrationParam);
                            TemplateControl .Save2DB(calibrationParam);
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
                            TemplateControl.Save2DB(aoiParam);
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
                            CreateNewTemplate(PGParam.Params, name, pGParam);
                            TemplateControl.Save2DB(pGParam);
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
                            CreateNewTemplate(SMUParam.Params, name, sMUParam);
                            TemplateControl.Save2DB(sMUParam);
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
                            CreateNewTemplate(PoiParam.Params, name, poiParam);
                            PoiParam.Save2DB(poiParam);
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
                            CreateNewTemplate(MTFParam.MTFParams, name, mTFParam);
                            TemplateControl.Save2DB(mTFParam);
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
                            CreateNewTemplate(SFRParam.SFRParams, name, sFRParam);
                            TemplateControl.Save2DB(sFRParam);
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
                            CreateNewTemplate(FOVParam.FOVParams, name, fOVParam);
                            TemplateControl.Save2DB(fOVParam);
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
                            CreateNewTemplate(GhostParam.GhostParams, name, ghostParam);
                            TemplateControl.Save2DB(ghostParam);
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
                            CreateNewTemplate(DistortionParam.DistortionParams, name, distortionParam);
                            TemplateControl.Save2DB(distortionParam);
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
                            CreateNewTemplate(LedCheckParam.LedCheckParams, name, ledCheckParam);
                            TemplateControl.Save2DB(ledCheckParam);
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
                            CreateNewTemplate(FocusPointsParam.FocusPointsParams, name, focusPointsParam);
                            TemplateControl.Save2DB(focusPointsParam);
                        }
                        else MessageBox.Show("数据库创建FocusPoints模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    }
                    break;
                case TemplateType.BuildPOIParmam:
                    if (true)
                    {
                        System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
                        ofd.Filter = "*.cfg|*.cfg";

                        if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                        string name = Path.GetFileNameWithoutExtension(ofd.FileName);
                        BuildPOIParam? buildPOIParam = JsonConvert.DeserializeObject<BuildPOIParam>(File.ReadAllText(ofd.FileName));
                        if (buildPOIParam != null)
                        {
                            CreateNewTemplate(BuildPOIParam.BuildPOIParams, name, buildPOIParam);
                            TemplateControl.Save2DB(buildPOIParam);
                        }
                        else MessageBox.Show("数据库创建BuildPOI模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    }
                    break;
                case TemplateType.SensorHeYuan:
                    if (true)
                    {
                        System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
                        ofd.Filter = "*.cfg|*.cfg";

                        if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                        string name = Path.GetFileNameWithoutExtension(ofd.FileName);
                        SensorHeYuan? sensorHeYuan = JsonConvert.DeserializeObject<SensorHeYuan>(File.ReadAllText(ofd.FileName));
                        if (sensorHeYuan != null)
                        {
                            CreateNewTemplate(SensorHeYuan.SensorHeYuans, name, sensorHeYuan);
                                TemplateControl.Save2DB(sensorHeYuan);
                        }
                        else MessageBox.Show("数据库创建SensorHeYuan模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    }
                    break;
                case TemplateType.CameraExposureParam:
                    if (true)
                    {
                        System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
                        ofd.Filter = "*.cfg|*.cfg";

                        if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                        string name = Path.GetFileNameWithoutExtension(ofd.FileName);
                        CameraExposureParam? cameraExposureParam = JsonConvert.DeserializeObject<CameraExposureParam>(File.ReadAllText(ofd.FileName));
                        if (cameraExposureParam != null)
                        {
                            CreateNewTemplate(CameraExposureParam.CameraExposureParams, name, cameraExposureParam);
                            TemplateControl.Save2DB(cameraExposureParam);
                        }
                        else MessageBox.Show("数据库创建CameraExposureParam模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    }
                    break;
                default:
                    break;
            }
        }

        public ObservableCollection<TemplateModelBase> TemplateModelBaseResults { get; set; } = new ObservableCollection<TemplateModelBase>();


        private void Searchbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (SearchNoneText.Visibility == Visibility.Visible)
                    SearchNoneText.Visibility = Visibility.Hidden;
                if (string.IsNullOrEmpty(textBox.Text))
                {
                    ListView1.ItemsSource = ITemplate.ItemsSource;

                }
                else
                {
                    TemplateModelBaseResults = new ObservableCollection<TemplateModelBase>();
                    foreach (var item in ITemplate.ItemsSource)
                    {
                        if (item is TemplateModelBase template)
                        {
                            if (template.Key.Contains(textBox.Text))
                                TemplateModelBaseResults.Add(template);
                        }
                    }
                    ListView1.ItemsSource = TemplateModelBaseResults;
                    if (TemplateModelBaseResults.Count == 0)
                    {
                        SearchNoneText.Visibility = Visibility.Visible;
                        SearchNoneText.Text = "未找到" + textBox.Text + "相关模板";
                    }
                }
            }
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && contextMenu.Items.Count == 0 && ListView1.View is GridView gridView)
                GridViewColumnVisibility.GenContentMenuGridViewColumn(contextMenu, gridView.Columns, GridViewColumnVisibilitys);
        }
        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {
            if (sender is GridViewColumnHeader gridViewColumnHeader && gridViewColumnHeader.Content != null && ListView1.ItemsSource is ObservableCollection<TemplateModelBase> results)
            {
                foreach (var item in GridViewColumnVisibilitys)
                {
                    if (item.ColumnName.ToString() == gridViewColumnHeader.Content.ToString())
                    {
                        if (item.ColumnName.ToString() == Resource.SerialNumber1)
                        {
                            item.IsSortD = !item.IsSortD;
                            results.SortByID(item.IsSortD);
                        }
                    }
                }
            }
        }

    }
}
