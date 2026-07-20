#pragma warning disable CA1822,CA1863,CS8602
using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.Devices.Camera.Dao;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam;
using ColorVision.Engine.Services.Devices.Camera.Views;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.ImageEditor.Settings;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Extension;
using ColorVision.UI.LogImp;
using cvColorVision;
using log4net;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;


namespace ColorVision.Engine.Services.Devices.Camera
{
    internal sealed record DeviceCameraCalibrationFile(
        string SlotKey,
        CalibrationType CalibrationType,
        string DisplayName,
        string RelativePath,
        string FullPath);

    public class DeviceCamera : DeviceService<ConfigCamera>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DeviceCamera));

        public PhyCamera? PhyCamera { get => _PhyCamera; set { _PhyCamera = value; OnPropertyChanged(); } }
        private PhyCamera? _PhyCamera;
        private readonly Lazy<ViewCamera> _view;
        public ViewCamera View => _view.Value;
        public MQTTCamera DService { get; set; }
        public RelayCommand FetchLatestTemperatureCommand { get; set; }

        public DisplayCameraConfig DisplayConfig => DisplayConfigManager.Instance.GetDisplayConfig<DisplayCameraConfig>(Config.Code);



        public DeviceCamera(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTCamera(this);
            _view = new Lazy<ViewCamera>(() => Application.Current.Dispatcher.CheckAccess()
                ? new ViewCamera(this)
                : Application.Current.Dispatcher.Invoke(() => new ViewCamera(this)));
            this.SetIconResource("DrawingImageCamera");

            EditCommand = new RelayCommand(a => EditCameraAction() ,b => AccessControl.Check(EditCameraAction));

            FetchLatestTemperatureCommand =  new RelayCommand(a => FetchLatestTemperature(a));

            DisplayCameraControlLazy = new Lazy<DisplayCamera>(() => new DisplayCamera(this));


            RefreshDeviceIdCommand = new RelayCommand(a => RefreshDeviceId());

            OpenPhyCameraMangerCommand = new RelayCommand(a => OpenPhyCameraManger());

            PhyCamera = PhyCameraManager.GetInstance().GetPhyCamera(Config.CameraCode);
            if (PhyCamera != null)
            {
                PhyCamera.ConfigChanged += PhyCameraConfigChanged;
                PhyCamera.DeviceCamera = this;
            }

            EditAutoExpTimeCommand = new RelayCommand(a => EditAutoExpTime());

            EditAutoFocusCommand = new RelayCommand(a => EditAutoFocus());
            EditCameraExpousureCommand = new RelayCommand(A => EditCameraExpousure());
            EditRealtimeCameraConfigCommand = new RelayCommand(_ => EditRealtimeCameraConfig());
            EditCalibrationCommand = new RelayCommand(a => EditCalibration());
            OpenCameraLogCommand = new RelayCommand(a => OpenCameraLog());

            this.ContextMenu.Items.Add(new MenuItem
            {
                Header = "Log",
                Command = new RelayCommand(_ => FlowEngineManager.GetInstance().WindowsServiceX64.OpenLog())
            });
            this.ContextMenu.Items.Add(new MenuItem() { Header = "CameraLog", Command = OpenCameraLogCommand });


            MenuItem menuItem = new MenuItem() { Header = "Local" };
            menuItem.Click += (s, e) =>
            {
                if (!File.Exists($"lincense\\{Config.CameraCode}.lic"))
                {
                    LicenseManagerViewModel licenseManagerViewModel  = new LicenseManagerViewModel();
                    licenseManagerViewModel.SaveToLincense();
                }

                CameraLocalWindow cameraLocalWindow = new CameraLocalWindow(this);
                cameraLocalWindow.Show();
            };

            ContextMenu.Items.Add(menuItem);

        }

        [CommandDisplay("CameraLog")]
        public RelayCommand OpenCameraLogCommand { get; set; }

        public void OpenCameraLog()
        {
            string baseDir = Directory.GetParent(ServiceConfig.Instance.CVMainService_x64).FullName;
            string latestLogPath = LogFileHelper.GetMostRecentLogFile(Path.Combine(baseDir, "log"), "CVMainWindowsService_x64_camera");
            if (!string.IsNullOrEmpty(latestLogPath))
            {
                WindowLogLocal windowLogLocal = new WindowLogLocal(latestLogPath, Encoding.GetEncoding("GB2312"));
                windowLogLocal.Show();
            }
        }


        [CommandDisplay("EditCalibrationFile")]
        public RelayCommand EditCalibrationCommand { get; set; }

        public void EditCalibration()
        {
            if (PhyCamera == null)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.ConfigurePhysicalCameraBeforeCalibration, "ColorVision");
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
        [CommandDisplay("AutoFocusTemplate",Order =100)]
        public RelayCommand EditAutoFocusCommand { get; set; }
        public static void EditAutoFocus()
        {
            var windowTemplate = new TemplateEditorWindow(new TemplateAutoFocus()) { Owner = Application.Current.GetActiveWindow() };
            windowTemplate.ShowDialog();
        }
        [CommandDisplay("CameraParameterTemplate",Order =100)]
        public RelayCommand EditCameraExpousureCommand { get; set; }
        
        public static void EditCameraExpousure()
        {
            var windowTemplate = new TemplateEditorWindow(new TemplateCameraRunParam()) { Owner = Application.Current.GetActiveWindow() };
            windowTemplate.ShowDialog();
        }



        public DefaultRealtimeCameraConfig RealtimeCameraConfig { get; } = DefaultRealtimeCameraConfig.Current;
        public RelayCommand EditRealtimeCameraConfigCommand { get; set; }

        private void EditRealtimeCameraConfig()
        {
            new PropertyEditorWindow(RealtimeCameraConfig)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.ShowDialog();
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
            e.ApplyTo(Config);

            DisplayConfig.Gain = e.CameraParameterLimit.GainDefault;
            DisplayConfig.ExpTime = e.CameraParameterLimit.ExpDefalut;
            DisplayConfig.ExpTimeR = e.CameraParameterLimit.ExpDefalut;
            DisplayConfig.ExpTimeG = e.CameraParameterLimit.ExpDefalut;
            DisplayConfig.ExpTimeB = e.CameraParameterLimit.ExpDefalut;
            Save();
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
            }  
            PhyCameraManagerWindow phyCameraManager = new PhyCameraManagerWindow() { Owner = Application.Current.GetActiveWindow() ,WindowStartupLocation =WindowStartupLocation.CenterOwner};
            phyCameraManager.ShowDialog();
        }

        [CommandDisplay("RefreshDeviceList")]
        public RelayCommand RefreshDeviceIdCommand { get; set; }
        public void RefreshDeviceId()
        {
            PhyCameraManager.GetInstance().SearchCameraIds();
        }

        public override void Save()
        {
            PhyCamera = PhyCameraManager.GetInstance().GetPhyCamera(Config.CameraCode);
            if (PhyCamera != null)
            {
                PhyCamera.SetDeviceCamera(this);

                PhyCamera.Config.ApplyTo(Config);

                OnPropertyChanged(nameof(PhyCamera));
            }
            base.Save();
        }

        private void FetchLatestTemperature(object a)
        {
            try
            {
                var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

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
                    MessageBox1.Show(Application.Current.MainWindow, ColorVision.Engine.Properties.Resources.TemperatureDataNotFound);
                }
            }
            catch (Exception ex)
            {
                MessageBox1.Show(Application.Current.MainWindow, ColorVision.Engine.Properties.Resources.ErrorQueryingTemperatureData+" : " + ex.Message);
            }
        }

        public override UserControl GetDeviceInfo() => new InfoCamera(this);
        
        public Lazy<DisplayCamera> DisplayCameraControlLazy { get; set; }

        public override UserControl GetDisplayControl() => DisplayCameraControlLazy.Value;

        public DisplayCamera GetDisplayCamera()=> new DisplayCamera(this);

        internal bool TryGetCalibrationTemplateFiles(CalibrationParam? param, out IReadOnlyList<DeviceCameraCalibrationFile> calibrationFiles, out string? errorMessage)
        {
            calibrationFiles = Array.Empty<DeviceCameraCalibrationFile>();
            errorMessage = null;

            if (param == null || param.Id == -1)
            {
                return true;
            }

            if (PhyCamera == null)
            {
                errorMessage = Properties.Resources.PhysicalCameraNotConfigured;
                return false;
            }

            GroupResource? groupResource = PhyCamera.VisualChildren
                .OfType<GroupResource>()
                .FirstOrDefault(resource => resource.Name == param.CalibrationMode);
            groupResource?.SetCalibrationResource();

            bool hasSelectedCalibration = CalibrationSlotDefinitions.AllSlots.Any(slot => slot.ParamGetter(param).IsSelected);
            if (groupResource == null || !hasSelectedCalibration)
            {
                errorMessage = string.Format(Properties.Resources.CalibrationFileNotConfiguredWithTemplate, param.Name);
                return false;
            }

            List<DeviceCameraCalibrationFile> resolvedFiles = new();
            foreach (var slot in CalibrationSlotDefinitions.AllSlots)
            {
                CalibrationBase selectedCalibration = slot.ParamGetter(param);
                if (!selectedCalibration.IsSelected)
                {
                    continue;
                }

                CalibrationResource? resource = slot.GroupGetter(groupResource);
                if (resource == null || !TryResolveCalibrationFilePath(resource, out string fullPath, out string relativePath))
                {
                    string displayName = resource?.Name ?? slot.Key;
                    errorMessage = string.Format(Properties.Resources.TemplateFileNotExist, param.Name, displayName);
                    return false;
                }

                resolvedFiles.Add(new DeviceCameraCalibrationFile(
                    slot.Key,
                    slot.ServiceType.ToCalibrationType(),
                    resource.Name,
                    relativePath,
                    fullPath));
            }

            calibrationFiles = resolvedFiles;
            return true;
        }

        internal bool TryResolveCalibrationFilePath(CalibrationResource resource, out string fullPath, out string relativePath)
        {
            fullPath = string.Empty;
            relativePath = string.Empty;

            if (PhyCamera == null || !Directory.Exists(PhyCamera.Config.FileServerCfg.FileBasePath))
            {
                return false;
            }

            relativePath = resource.SysResourceModel.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return false;
            }

            fullPath = Path.Combine(PhyCamera.Config.FileServerCfg.FileBasePath, PhyCamera.Code, "cfg", relativePath);
            return File.Exists(fullPath);
        }

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
