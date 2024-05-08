using ColorVision.Common.Extension;
using ColorVision.Common.MVVM;
using ColorVision.Common.Sorts;
using ColorVision.Common.Utilities;
using ColorVision.Extension;
using ColorVision.Properties;
using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Algorithm.Templates;
using ColorVision.Services.Devices.Camera;
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
using cvColorVision;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Services.Templates
{
    public interface ITemplate
    {
        public void Load();
        public void Create();
        public void Save();
        public void Delete();
        public void Import();
        public void Export();
    }



    /// <summary>
    /// CalibrationTemplate.xaml 的交互逻辑
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
                        FlowParam.LoadFlowParam();
                    TemplateModelBases = TemplateControl.GetTemplateModelBases(FlowParam.Params);
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
                    {
                        TemplateControl.LoadModCabParam(DeviceCamera.CalibrationParams, DeviceCamera.SysResourceModel.Id, ModMasterType.Calibration);
                    }
                    TemplateModelBases = TemplateControl.GetTemplateModelBases(DeviceCamera.CalibrationParams);
                    Title = "校正参数设置";
                    break;
                case TemplateType.SpectrumResourceParam:
                    if (IsReLoad)
                    {
                        TemplateControl.LoadModCabParam(DeviceSpectrum.SpectrumResourceParams, DeviceSpectrum.SysResourceModel.Id, ModMasterType.SpectrumResource);
                    }
                    TemplateModelBases = TemplateControl.GetTemplateModelBases(DeviceSpectrum.SpectrumResourceParams);
                    Title = "校正参数设置";
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
                        
                        TemplateControl.LoadParams(PoiParam.Params);
                    TemplateModelBases = TemplateControl.GetTemplateModelBases(PoiParam.Params);
                    Title = "关注点设置";
                    break;
                case TemplateType.MTFParam:
                    if (IsReLoad)
                        TemplateControl.LoadParams(MTFParam.MTFParams);
                    TemplateModelBases = TemplateControl.GetTemplateModelBases(MTFParam.MTFParams);
                    Title = "MTF算法设置";
                    break;
                case TemplateType.SFRParam:
                    if (IsReLoad)
                        TemplateControl.LoadParams(SFRParam.SFRParams);
                    TemplateModelBases = TemplateControl.GetTemplateModelBases(SFRParam.SFRParams);
                    Title = "SFR算法设置";
                    break;
                case TemplateType.FOVParam:
                    if (IsReLoad)
                        TemplateControl.LoadParams(FOVParam.FOVParams);
                    TemplateModelBases = TemplateControl.GetTemplateModelBases(FOVParam.FOVParams);
                    Title = "FOV算法设置";
                    break;
                case TemplateType.GhostParam:
                    if (IsReLoad)
                        TemplateControl.LoadParams(GhostParam.GhostParams);
                    TemplateModelBases = TemplateControl.GetTemplateModelBases(GhostParam.GhostParams);
                    Title = "鬼影算法设置";
                    break;
                case TemplateType.DistortionParam:
                    if (IsReLoad)
                        TemplateControl.LoadParams(DistortionParam.DistortionParams);
                    TemplateModelBases = TemplateControl.GetTemplateModelBases(DistortionParam.DistortionParams);
                    Title = "畸变算法设置";
                    break;
                case TemplateType.LedCheckParam:
                    if (IsReLoad)
                        TemplateControl.LoadParams(LedCheckParam.LedCheckParams);
                    TemplateModelBases = TemplateControl.GetTemplateModelBases(LedCheckParam.LedCheckParams);
                    Title = "灯光检测算法设置";
                    break;
                case TemplateType.FocusPointsParam:
                    if (IsReLoad)
                        TemplateControl.LoadParams(FocusPointsParam.FocusPointsParams);
                    TemplateModelBases = TemplateControl.GetTemplateModelBases(FocusPointsParam.FocusPointsParams);
                    Title = "FocusPoints算法设置";
                    break;
                case TemplateType.BuildPOIParmam:
                    if (IsReLoad)
                        TemplateControl.LoadParams(BuildPOIParam.BuildPOIParams);
                    TemplateModelBases = TemplateControl.GetTemplateModelBases(BuildPOIParam.BuildPOIParams);
                    Title = "BuildPOI算法设置";
                    break;
                case TemplateType.SensorHeYuan:
                    if (IsReLoad)
                        TemplateControl.LoadParams(SensorHeYuan.SensorHeYuans);
                    TemplateModelBases = TemplateControl.GetTemplateModelBases(SensorHeYuan.SensorHeYuans);
                    Title = "SensorHeYuan算法设置";
                    break;
                case TemplateType.CameraExposureParam:
                    if (IsReLoad)
                        TemplateControl.LoadParams(CameraExposureParam.CameraExposureParams);
                    TemplateModelBases = TemplateControl.GetTemplateModelBases(CameraExposureParam.CameraExposureParams);
                    Title = "相机曝光参数设置";
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
            Load(windowTemplateType, IsReLoad);
            InitializeComponent();
            GridProperty.Children.Clear();
            GridProperty.Margin = new Thickness(5, 5, 5, 5);
            GridProperty.Children.Add(UserControl);
            Width = Width + 200;
        }



        public DeviceSpectrum DeviceSpectrum { get; set; }
        public WindowTemplate(TemplateType windowTemplateType, UserControl userControl, DeviceSpectrum deviceSpectrum, bool IsReLoad = true)
        {
            DeviceSpectrum = deviceSpectrum;
            TemplateType = windowTemplateType;
            TemplateControl = TemplateControl.GetInstance();
            UserControl = userControl;
            Load(windowTemplateType, IsReLoad);
            InitializeComponent();
            GridProperty.Children.Clear();
            GridProperty.Margin = new Thickness(5, 5, 5, 5);
            GridProperty.Children.Add(UserControl);
        }


        private void Window_Initialized(object sender, EventArgs e)
        {
            ListView1.ItemsSource = TemplateModelBases;
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
                        if (TemplateModelBases[listView.SelectedIndex].GetValue() is PoiParam poiParam)
                        {
                            var WindowFocusPoint = new WindowFocusPoint(poiParam) { Owner = this };
                            WindowFocusPoint.Closed += async (s, e) =>
                            {
                                await Task.Delay(30);
                                TemplateModelBases[listView.SelectedIndex].Tag = $"{poiParam.Width}*{poiParam.Height}{(ConfigHandler.GetInstance().SoftwareConfig.IsUseMySql ? "" : $"_{poiParam.PoiPoints.Count}")}";
                            };
                            WindowFocusPoint.Show();
                        }
                        break;
                    case TemplateType.FlowParam:
                        if (TemplateModelBases[listView.SelectedIndex].GetValue() is FlowParam flowParam)
                        {
                            flowParam.Name ??= TemplateModelBases[listView.SelectedIndex].Key;
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
                        if (UserControl is CalibrationControl calibration && TemplateModelBases[listView.SelectedIndex].GetValue() is CalibrationParam calibrationParam)
                        {
                            calibration.Initializedsss(DeviceCamera, calibrationParam);
                        }
                        break;
                    case TemplateType.SpectrumResourceParam:
                        if (UserControl is SpectrumResourceControl spectrumResourceControl && TemplateModelBases[listView.SelectedIndex].GetValue() is SpectrumResourceParam spectrumResourceParam)
                        {
                            spectrumResourceControl.Initializedsss(DeviceSpectrum, spectrumResourceParam);
                        }
                        break;
                    case TemplateType.MeasureParam:
                        if (UserControl is MeasureParamControl mpc && TemplateModelBases[listView.SelectedIndex].GetValue() is MeasureParam mp)
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
                        PropertyGrid1.SelectedObject = TemplateModelBases[listView.SelectedIndex].GetValue();
                        break;
                }
            }
        }

        private void CreateNewTemplateFromDB(string TemplateName)
        {
            switch (TemplateType)
            {
                case TemplateType.Calibration:
                    CalibrationParam? CalibrationParam = TemplateControl.AddCalibrationParam<CalibrationParam>(ModMasterType.Calibration, TemplateName,DeviceCamera.SysResourceModel.Id);
                    if (CalibrationParam != null) CreateNewTemplate(DeviceCamera.CalibrationParams, TemplateName, CalibrationParam);
                    else MessageBox.Show("数据库创建CalibrationParam模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    break;
                case TemplateType.SpectrumResourceParam:
                    SpectrumResourceParam? SpectrumResourceParam = TemplateControl.AddCalibrationParam<SpectrumResourceParam>(ModMasterType.SpectrumResource, TemplateName, DeviceSpectrum.SysResourceModel.Id);
                    if (SpectrumResourceParam != null) CreateNewTemplate(DeviceSpectrum.SpectrumResourceParams, TemplateName, SpectrumResourceParam);
                    else MessageBox.Show("数据库创建SpectrumResourceParams模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    break;
                case TemplateType.AoiParam:
                    AOIParam? aoiParam = TemplateControl.AddParamMode<AOIParam>(ModMasterType.Aoi, TemplateName);
                    if (aoiParam != null) CreateNewTemplate(TemplateControl.AoiParams, TemplateName, aoiParam);
                    else MessageBox.Show("数据库创建AOI模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    break;
                case TemplateType.PGParam:
                    PGParam? pgParam = TemplateControl.AddParamMode<PGParam>(ModMasterType.PG, TemplateName);
                    if (pgParam != null) CreateNewTemplate(TemplateControl.PGParams, TemplateName, pgParam);
                    else MessageBox.Show("数据库创建PG模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    break;
                case TemplateType.SMUParam:
                    SMUParam?  sMUParam = TemplateControl.AddParamMode<SMUParam>(ModMasterType.SMU, TemplateName);
                    if (sMUParam != null) CreateNewTemplate(TemplateControl.SMUParams, TemplateName, sMUParam);
                    else MessageBox.Show("数据库创建源表模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    break;
                case TemplateType.MTFParam:
                    MTFParam? mTFParam = TemplateControl.AddParamMode<MTFParam>(ModMasterType.MTF, TemplateName);
                    if (mTFParam != null) CreateNewTemplate(MTFParam.MTFParams, TemplateName, mTFParam);
                    else MessageBox.Show("数据库创建MTF模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    break;
                case TemplateType.SFRParam:
                    SFRParam? sFRParam = TemplateControl.AddParamMode<SFRParam>(ModMasterType.SFR, TemplateName);
                    if (sFRParam != null) CreateNewTemplate(SFRParam.SFRParams, TemplateName, sFRParam);
                    else MessageBox.Show("数据库创建MTF模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly); break;
                case TemplateType.FOVParam:
                    FOVParam? fOVParam = TemplateControl.AddParamMode<FOVParam>(ModMasterType.FOV, TemplateName);
                    if (fOVParam != null) CreateNewTemplate(FOVParam.FOVParams, TemplateName, fOVParam);
                    else MessageBox.Show("数据库创建FOV模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly); break;
                case TemplateType.GhostParam:
                    GhostParam? ghostParam = TemplateControl.AddParamMode<GhostParam>(ModMasterType.Ghost, TemplateName);
                    if (ghostParam != null) CreateNewTemplate(GhostParam.GhostParams, TemplateName, ghostParam);
                    else MessageBox.Show("数据库创建Ghost模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly); break;
                case TemplateType.DistortionParam:
                    DistortionParam? distortionParam = TemplateControl.AddParamMode<DistortionParam>(ModMasterType.Distortion, TemplateName);
                    if (distortionParam != null) CreateNewTemplate(DistortionParam.DistortionParams, TemplateName, distortionParam);
                    else MessageBox.Show("数据库创建Distortion模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly); break;
                case TemplateType.FocusPointsParam:
                    FocusPointsParam? focusPointsParam = TemplateControl.AddParamMode<FocusPointsParam>(ModMasterType.FocusPoints,TemplateName);
                    if (focusPointsParam != null) CreateNewTemplate(FocusPointsParam.FocusPointsParams, TemplateName, focusPointsParam);
                    else MessageBox.Show("数据库创建FocusPoints模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    break;
                case TemplateType.LedCheckParam:
                    LedCheckParam? ledCheckParam = TemplateControl.AddParamMode<LedCheckParam>(ModMasterType.LedCheck, TemplateName);
                    if (ledCheckParam != null) CreateNewTemplate(LedCheckParam.LedCheckParams, TemplateName, ledCheckParam);
                    else MessageBox.Show("数据库创建灯光检测模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
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
                    MeasureParam? measureParam = TemplateControl.AddMeasureParam(TemplateName);
                    if (measureParam != null) CreateNewTemplate(TemplateControl.MeasureParams, TemplateName, measureParam);
                    else MessageBox.Show("数据库创建流程模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    break;
                case TemplateType.BuildPOIParmam:
                    BuildPOIParam? buildPOIParam = TemplateControl.AddParamMode<BuildPOIParam>(ModMasterType.BuildPOI, TemplateName);
                    if (buildPOIParam != null) CreateNewTemplate(BuildPOIParam.BuildPOIParams, TemplateName, buildPOIParam);
                    else MessageBox.Show("数据库创建BuildPOI模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    break;
                case TemplateType.CameraExposureParam:
                    CameraExposureParam? cameraExposureParam = TemplateControl.AddParamMode<CameraExposureParam>(ModMasterType.CameraExposure, TemplateName);
                    if (cameraExposureParam != null) CreateNewTemplate(CameraExposureParam.CameraExposureParams, TemplateName, cameraExposureParam);
                    else MessageBox.Show("数据库创建CameraExposureParam模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                    break;
                case TemplateType.SensorHeYuan:
                    SensorHeYuan? sensorHeYuan = TemplateControl.AddParamMode<SensorHeYuan>(ModMasterType.SensorHeYuan, TemplateName);
                    if (sensorHeYuan != null) CreateNewTemplate(SensorHeYuan.SensorHeYuans, TemplateName, sensorHeYuan);
                    else MessageBox.Show("数据库创建SensorHeYuan模板失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
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
                case TemplateType.Calibration:
                    TemplateControl.Save2DB(DeviceCamera.CalibrationParams);
                    break;
                case TemplateType.SpectrumResourceParam:
                    TemplateControl.Save2DB(DeviceSpectrum.SpectrumResourceParams);
                    break;
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
                case TemplateType.AoiParam:
                    TemplateControl.Save2DB(TemplateControl.AoiParams);
                    break;
                case TemplateType.PGParam:
                    TemplateControl.Save2DB(TemplateControl.PGParams);
                    break;
                case TemplateType.SMUParam:
                    TemplateControl.Save2DB(TemplateControl.SMUParams);
                    break;
                case TemplateType.MTFParam:
                    TemplateControl.Save2DB(MTFParam.MTFParams);
                    break;
                case TemplateType.SFRParam:
                    TemplateControl.Save2DB(SFRParam.SFRParams);
                    break;
                case TemplateType.FOVParam:
                    TemplateControl.Save2DB(FOVParam.FOVParams);
                    break;
                case TemplateType.GhostParam:
                    TemplateControl.Save2DB(GhostParam.GhostParams);
                    break;
                case TemplateType.DistortionParam:
                    TemplateControl.Save2DB(DistortionParam.DistortionParams);
                    break;
                case TemplateType.FocusPointsParam:
                    TemplateControl.Save2DB(FocusPointsParam.FocusPointsParams);
                    break;
                case TemplateType.LedCheckParam:
                    TemplateControl.Save2DB(LedCheckParam.LedCheckParams);
                    break;
                case TemplateType.BuildPOIParmam:
                    TemplateControl.Save2DB(BuildPOIParam.BuildPOIParams);
                    break;
                case TemplateType.SensorHeYuan:
                    TemplateControl.Save2DB(SensorHeYuan.SensorHeYuans);
                    break;
                case TemplateType.CameraExposureParam:
                    TemplateControl.Save2DB(CameraExposureParam.CameraExposureParams);
                    break;
                default:
                    break;
            }

        }


        public void TemplateNew()
        {
            CreateTemplate createWindow = new CreateTemplate(TemplateModelBases,TemplateType) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
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
            keyValuePairs.Add(new TemplateModel<T>(Name, t));
            TemplateModel<T> config = new TemplateModel<T> {Value = t, Key = Name, };
            TemplateModelBases.Add(config);
            ListView1.SelectedIndex = TemplateModelBases.Count - 1;
            ListView1.ScrollIntoView(config);
        }

        private ModMasterDao masterFlowDao = new ModMasterDao(ModMasterType.Flow);
        private ModMasterDao masterModDao = new ModMasterDao();

        private ModDetailDao detailDao = new ModDetailDao();
        private VSysResourceDao resourceDao = new VSysResourceDao();

        public void TemplateDel()
        {

            void MasterDeleteById(int id)
            {
                List<ModDetailModel> de = detailDao.GetAllByPid(id);
                int ret = masterFlowDao.DeleteById(id);
                detailDao.DeleteAllByPid(id);
                if (de != null && de.Count > 0)
                {
                    string[] codes = new string[de.Count];
                    int idx = 0;
                    foreach (ModDetailModel model in de)
                    {
                        string code = model.GetValueMD5();
                        codes[idx++] = code;
                    }
                    resourceDao.DeleteInCodes(codes);
                }
            }
            void TemplateDel<T>(ObservableCollection<TemplateModel<T>> keyValuePairs) where T : ParamBase
            {
                if (ConfigHandler.GetInstance().SoftwareConfig.IsUseMySql)
                {
                    MasterDeleteById(keyValuePairs[ListView1.SelectedIndex].Value.Id);
                }
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
                            TemplateDel(DeviceCamera.CalibrationParams);
                            break;
                        case TemplateType.SpectrumResourceParam:
                            TemplateDel(DeviceSpectrum.SpectrumResourceParams);
                            break;
                        case TemplateType.PGParam:
                            TemplateDel(TemplateControl.PGParams);
                            break;
                        case TemplateType.SMUParam:
                            TemplateDel(TemplateControl.SMUParams);
                            break;
                        case TemplateType.PoiParam:
                            PoiMasterDao poiMasterDao = new PoiMasterDao();
                            poiMasterDao.DeleteById(PoiParam.Params[ListView1.SelectedIndex].Value.Id);
                            PoiParam.Params.RemoveAt(ListView1.SelectedIndex);
                            break;
                        case TemplateType.MTFParam:
                            TemplateDel(MTFParam.MTFParams);
                            break;
                        case TemplateType.SFRParam:
                            TemplateDel(SFRParam.SFRParams);
                            break;
                        case TemplateType.FOVParam:
                            TemplateDel(FOVParam.FOVParams);
                            break;
                        case TemplateType.GhostParam:
                            TemplateDel(GhostParam.GhostParams);
                            break;
                        case TemplateType.DistortionParam:
                            TemplateDel(DistortionParam.DistortionParams);
                            break;
                        case TemplateType.FocusPointsParam:
                            TemplateDel(FocusPointsParam.FocusPointsParams);
                            break;
                        case TemplateType.LedCheckParam:
                            TemplateDel(LedCheckParam.LedCheckParams);
                            break;
                        case TemplateType.FlowParam:
                            TemplateDel(FlowParam.Params);
                            break;
                        case TemplateType.MeasureParam:
                            TemplateDel(TemplateControl.MeasureParams);
                            break;
                        case TemplateType.BuildPOIParmam:
                            TemplateDel(BuildPOIParam.BuildPOIParams);
                            break;
                        case TemplateType.CameraExposureParam:
                            TemplateDel(CameraExposureParam.CameraExposureParams);
                            break;
                        case TemplateType.SensorHeYuan:
                            TemplateDel(SensorHeYuan.SensorHeYuans);
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
                        FlowParam? flowParam = FlowParam.AddFlowParam(name);
                        if (flowParam != null)
                        {
                            flowParam.DataBase64 = Tool.FileToBase64(ofd.FileName); ;
                            CreateNewTemplate(FlowParam.Params, name, flowParam);

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
                        CalibrationParam? calibrationParam = TemplateControl.AddParamMode<CalibrationParam>(ModMasterType.Calibration, name);
                        if (calibrationParam1 != null && calibrationParam!=null)
                            calibrationParam1.CopyTo(calibrationParam);
                        if (calibrationParam != null)
                        {
                            CreateNewTemplate(DeviceCamera.CalibrationParams, name, calibrationParam);
                            TemplateControl.GetInstance().Save2DB(calibrationParam);
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
                            CreateNewTemplate(SFRParam.SFRParams, name, sFRParam);
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
                            CreateNewTemplate(FOVParam.FOVParams, name, fOVParam);
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
                            CreateNewTemplate(GhostParam.GhostParams, name, ghostParam);
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
                            CreateNewTemplate(DistortionParam.DistortionParams, name, distortionParam);
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
                            CreateNewTemplate(LedCheckParam.LedCheckParams, name, ledCheckParam);
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
                            CreateNewTemplate(FocusPointsParam.FocusPointsParams, name, focusPointsParam);
                            TemplateControl.GetInstance().Save2DB(focusPointsParam);
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
                            TemplateControl.GetInstance().Save2DB(buildPOIParam);
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
                            TemplateControl.GetInstance().Save2DB(sensorHeYuan);
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
                            TemplateControl.GetInstance().Save2DB(cameraExposureParam);
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
                    ListView1.ItemsSource = TemplateModelBases;

                }
                else
                {
                    TemplateModelBaseResults = new ObservableCollection<TemplateModelBase>();
                    foreach (var item in TemplateModelBases)
                    {
                        if (item.Key.Contains(textBox.Text))
                            TemplateModelBaseResults.Add(item);
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
