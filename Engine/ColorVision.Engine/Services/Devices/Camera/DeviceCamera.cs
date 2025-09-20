﻿using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.Devices.Camera.Dao;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam;
using ColorVision.Engine.Services.Devices.Camera.Video;
using ColorVision.Engine.Services.Devices.Camera.Views;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using cvColorVision;
using log4net;
using SqlSugar;
using System;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Camera
{
    public class DeviceCamera : DeviceService<ConfigCamera>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DeviceCamera));

        public PhyCamera? PhyCamera { get => _PhyCamera; set { _PhyCamera = value; OnPropertyChanged(); } }
        private PhyCamera? _PhyCamera;
        public ViewCamera View { get; set; }
        public MQTTCamera DService { get; set; }
        public RelayCommand FetchLatestTemperatureCommand { get; set; }
        public RelayCommand DisPlaySaveCommand { get; set; }


        public DeviceCamera(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTCamera(Config);

            View = new ViewCamera(this);
            View.View.Title = $"相机视图 - {Config.Code}";
            this.SetIconResource("DrawingImageCamera", View.View);

            EditCommand = new RelayCommand(a => EditCameraAction() ,b => AccessControl.Check(EditCameraAction));

            FetchLatestTemperatureCommand =  new RelayCommand(a => FetchLatestTemperature(a));


            DisPlaySaveCommand = new RelayCommand(a => SaveDis());
            DisplayCameraControlLazy = new Lazy<DisplayCamera>(() => new DisplayCamera(this));


            RefreshDeviceIdCommand = new RelayCommand(a => RefreshDeviceId());

            OpenPhyCameraMangerCommand = new RelayCommand(a => OpenPhyCameraManger());

            PhyCamera = PhyCameraManager.GetInstance().GetPhyCamera(Config.CameraCode);
            if (PhyCamera != null)
            {
                PhyCamera.ConfigChanged += PhyCameraConfigChanged;
                PhyCamera.DeviceCamera = this;
            }

            RefreshCommand = new RelayCommand(a => RestartRCService());
            ServiceClearCommand = new RelayCommand(a => ServiceClear(), b => AccessControl.Check(ServiceClear));

            EditAutoExpTimeCommand = new RelayCommand(a => EditAutoExpTime());

            EditAutoFocusCommand = new RelayCommand(a => EditAutoFocus());
            EditCameraExpousureCommand = new RelayCommand(A => EditCameraExpousure());
            EditCalibrationCommand = new RelayCommand(a => EditCalibration());
        }
        [CommandDisplay("编辑校正文件")]
        public RelayCommand EditCalibrationCommand { get; set; }

        public void EditCalibration()
        {
            if (PhyCamera == null)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), "在使用校正前，请先配置对映的物理相机", "ColorVision");
                return;
            }
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox1.Show(Application.Current.MainWindow, Properties.Resources.DatabaseConnectionFailed, "ColorVision");
                return;
            }
            var ITemplate = new TemplateCalibrationParam(PhyCamera);
            var windowTemplate = new TemplateEditorWindow(ITemplate) { Owner = Application.Current.GetActiveWindow() };
            windowTemplate.ShowDialog();
        }


        [CommandDisplay("AutoExploreTemplate",Order =100)]
        public RelayCommand EditAutoExpTimeCommand { get; set; }

        public static void EditAutoExpTime()
        {
            var windowTemplate = new TemplateEditorWindow(new TemplateAutoExpTime()) { Owner = Application.Current.GetActiveWindow() };
            windowTemplate.ShowDialog();
        }
        [CommandDisplay("自动聚焦模板",Order =100)]
        public RelayCommand EditAutoFocusCommand { get; set; }
        public static void EditAutoFocus()
        {
            var windowTemplate = new TemplateEditorWindow(new TemplateAutoFocus()) { Owner = Application.Current.GetActiveWindow() };
            windowTemplate.ShowDialog();
        }
        [CommandDisplay("相机参数模板",Order =100)]
        public RelayCommand EditCameraExpousureCommand { get; set; }
        
        public static void EditCameraExpousure()
        {
            var windowTemplate = new TemplateEditorWindow(new TemplateCameraRunParam()) { Owner = Application.Current.GetActiveWindow() };
            windowTemplate.ShowDialog();
        }



        public VideoReader CameraVideoControl { get; set; } = new VideoReader();

        public new void RestartRCService()
        {
            if (DService.IsVideoOpen)
            {
                CameraVideoControl?.Close();
                var msgrecode = DService.Close();
                log.Info("正在关闭视频模式");
                msgrecode.MsgSucessed += (e) =>
                {
                    DService.IsVideoOpen = false;
                    DService.DeviceStatus = CVCommCore.DeviceStatusType.Closed;
                    base.RestartRCService();
                };
                return;
            }
            base.RestartRCService();
        }

        private PhyCamera lastPhyCamera;

        public void PhyCameraConfigChanged(object? sender, PhyCameras.Configs.ConfigPhyCamera e)
        {
            if (lastPhyCamera !=null && sender is PhyCamera phyCamera && phyCamera != lastPhyCamera)
            {
                lastPhyCamera.ConfigChanged -= PhyCameraConfigChanged;
                lastPhyCamera = phyCamera;
                lastPhyCamera.DeviceCamera = this;
                lastPhyCamera.DeviceCamera = null;
            }
            Config.Channel = e.Channel;
            Config.CFW.CopyFrom(e.CFW);
            Config.MotorConfig.CopyFrom(e.MotorConfig);
            Config.CameraID = e.CameraID;
            Config.CameraMode = e.CameraMode;
            Config.CameraType = e.CameraType;
            Config.CameraModel = e.CameraModel;
            Config.TakeImageMode = e.TakeImageMode;
            Config.ImageBpp = e.ImageBpp;
            Config.GainMin = e.CameraParameterLimit.GainMin;
            Config.GainMax = e.CameraParameterLimit.GainMax;
            Config.Gain = e.CameraParameterLimit.GainDefault;
            Config.ExpTimeMax = e.CameraParameterLimit.ExpMax;
            Config.ExpTimeMin = e.CameraParameterLimit.ExpMin;
            Config.ExpTime = e.CameraParameterLimit.ExpDefalut;
            Config.ExpTimeR = e.CameraParameterLimit.ExpDefalut;
            Config.ExpTimeG = e.CameraParameterLimit.ExpDefalut;
            Config.ExpTimeB = e.CameraParameterLimit.ExpDefalut;
            Save();
        }

        [CommandDisplay("清理服务缓存")]
        public RelayCommand ServiceClearCommand { get; set; }
        [RequiresPermission(PermissionMode.Administrator)]
        private void ServiceClear()
        {
            if (MessageBox1.Show(Application.Current.GetActiveWindow(), "文件删除后不可找回", "ColorVision", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
            {
                var MsgRecord = DService.ClearDataCache();
                MsgRecord.MsgSucessed += (s) =>
                {
                    MessageBox1.Show(Application.Current.GetActiveWindow(), "文件服务清理完成", "ColorVison");
                    MsgRecord.ClearMsgRecordSucessChangedHandler();
                };
            }
        }

        [RequiresPermission(PermissionMode.Administrator)]
        private void EditCameraAction()
        {
            EditCamera window = new(this);
            window.Owner = Application.Current.GetActiveWindow();
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();
        }
        [CommandDisplay("ManagePhysicalCamera")]
        public RelayCommand OpenPhyCameraMangerCommand { get; set; }

        [RequiresPermission(PermissionMode.Administrator)]
        public void OpenPhyCameraManger()
        {
            if (PhyCamera != null)
            {
                foreach (var item in PhyCameraManager.GetInstance().PhyCameras)
                {
                    item.IsSelected = false;
                }
                PhyCamera.IsSelected = true;
            }
            else
            {
                DService.GetAllCameraID();
            }  
            PhyCameraManagerWindow phyCameraManager = new PhyCameraManagerWindow() { Owner = Application.Current.GetActiveWindow() ,WindowStartupLocation =WindowStartupLocation.CenterOwner};
            phyCameraManager.ShowDialog();
        }

        [CommandDisplay("刷新设备列表")]
        public RelayCommand RefreshDeviceIdCommand { get; set; }
        private bool _isRefreshing = false;

        public void RefreshDeviceId()
        {
            if (EngineCongig.Instance.SuperMode)
            {
                if (_isRefreshing)
                {
                    MessageBox.Show("正在执行GetAllCameraID","ColorVision");
                    return; // 防止重复点击
                }
                _isRefreshing = true;
                //string strPathSysCfg = "cfg\\sys.cfg";
                //IntPtr m_hCamHandle = cvCameraCSLib.CM_CreatCameraManagerV1(CameraModel.QHY_USB, CameraMode.BV_MODE, strPathSysCfg);

                int bufferLength = 1024;
                StringBuilder snBuilder = new StringBuilder(bufferLength);

                //// 获取所有相机ID
                //int ret = cvCameraCSLib.GetAllCameraID(snBuilder, bufferLength);
                //log.Info($"GetAllCameraID 返回值: {ret}");
                //if (ret != 1)
                //{
                //    MessageBox.Show("获取相机ID失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                //    return;
                //}

                //string cameraIds = snBuilder.ToString();
                //// 获取所有相机ID的MD5
                //snBuilder.Clear();

                // 异步执行，避免阻塞UI线程
                Task.Run(() =>
                {
                    int ret = cvCameraCSLib.GetAllCameraIDMD5(snBuilder, bufferLength);
                    _isRefreshing = false;
                    // 回到UI线程
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        log.Info($"GetAllCameraIDMD5 返回值: {ret}");
                        if (ret == 1)
                        {
                            string cameraIdsMd5 = snBuilder.ToString();
                            MessageBox.Show(cameraIdsMd5, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("获取相机ID MD5失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    });
                });
                return;
            }
            if (PhyCamera !=null && PhyCamera.LicenseState != LicenseState.Licensed)
            {
                if ( MessageBox.Show(Application.Current.GetActiveWindow(), "当前逻辑相机许可证过期，无法刷新设备列表，是否清空当前相机服务绑定的物理相机，然后在重试", "ColorVision",MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    Config.CameraCode = string.Empty;
                    Save();
                    return;
                }
                else
                {
                    return;
                }
            }

            if (DService.DeviceStatus == CVCommCore.DeviceStatusType.OffLine)
            {
                if (MessageBox.Show(Application.Current.GetActiveWindow(), "当前逻辑相机离线，无法刷新设备列表，是否清空当前相机服务绑定的物理相机，然后在重试", "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    Config.CameraCode = string.Empty;
                    Save();
                    return;
                }
                else
                {
                    return;
                }
            }

            MsgRecord msgRecord =  DService.GetAllCameraID();
            msgRecord.MsgRecordStateChanged += (e) =>
            {
                if (e == MsgRecordState.Success)
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), "当前设备相机信息" + Environment.NewLine + msgRecord.MsgReturn.Data);
                    PhyCameraManager.GetInstance().LoadPhyCamera();
                    PhyCameraManager.GetInstance().RefreshEmptyCamera();
                }
                else
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), "刷新设备列表失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);

                }
            };
        }

        public void SaveDis()
        {
            if (MessageBox1.Show(Application.Current.GetActiveWindow(), "是否保存当前界面的曝光配置", "ColorVison", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            SaveConfig();
        }

        public override void Save()
        {
            PhyCamera = PhyCameraManager.GetInstance().GetPhyCamera(Config.CameraCode);
            if (PhyCamera != null)
            {
                PhyCamera.SetDeviceCamera(this);

                Config.Channel = PhyCamera.Config.Channel;
                Config.CFW.CopyFrom(PhyCamera.Config.CFW);
                Config.MotorConfig.CopyFrom(PhyCamera.Config.MotorConfig);
                Config.CameraID = PhyCamera.Config.CameraID;
                Config.CameraMode = PhyCamera.Config.CameraMode;
                Config.CameraType = PhyCamera.Config.CameraType;
                Config.CameraModel = PhyCamera.Config.CameraModel;
                Config.TakeImageMode = PhyCamera.Config.TakeImageMode;
                Config.ImageBpp = PhyCamera.Config.ImageBpp;

                OnPropertyChanged(nameof(PhyCamera));
            }
            base.Save();
        }

        private void FetchLatestTemperature(object a)
        {
            try
            {
                var list = Db.Queryable<CameraTempModel>()
                    .Where(x => x.RescourceId == SysResourceModel.Id)
                    .OrderBy(x => x.CreateDate, OrderByType.Desc)
                    .Take(100)
                    .ToList();

                if (list != null && list.Count > 0)
                {
                    list.Reverse(); // 如果需要按时间升序显示，保留 Reverse
                    TemperatureChartWindow window = new TemperatureChartWindow(list);
                    window.Show();
                }
                else
                {
                    MessageBox1.Show(Application.Current.MainWindow, "查询不到对应的温度数据");
                }
            }
            catch (Exception ex)
            {
                MessageBox1.Show(Application.Current.MainWindow, "查询温度数据时发生错误：" + ex.Message);
            }
        }

        public override UserControl GetDeviceInfo() => new InfoCamera(this);
        
        public Lazy<DisplayCamera> DisplayCameraControlLazy { get; set; }

        public override UserControl GetDisplayControl() => DisplayCameraControlLazy.Value;

        public override MQTTServiceBase? GetMQTTService()
        {
            return DService;
        }
        public override void Dispose()
        {
            this.PhyCamera?.ReleaseDeviceCamera();

            DService?.Dispose();
            base.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
