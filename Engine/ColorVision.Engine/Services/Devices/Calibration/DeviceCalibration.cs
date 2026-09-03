using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Calibration.Views;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Extension;
using ColorVision.UI.Views;
using log4net;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Calibration
{
    public class DeviceCalibration : DeviceService<ConfigCalibration>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DeviceCalibration));
        private bool _isDisposed;
        private PhyCamera? _subscribedPhyCamera;

        public MQTTCalibration DService { get; set; }

        public DisplayCalibrationConfig DisplayConfig => DisplayConfigManager.Instance.GetDisplayConfig<DisplayCalibrationConfig>(Config.Code);

        public PhyCamera? PhyCamera { get => PhyCameraManager.GetInstance().GetPhyCamera(Config.CameraCode); }

        private readonly Lazy<ViewCalibration> _view;
        public ViewCalibration View => _view.Value;

        public DeviceCalibration(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTCalibration(Config);
            _view = new Lazy<ViewCalibration>(() => Application.Current.Dispatcher.CheckAccess()
                ? new ViewCalibration(this)
                : Application.Current.Dispatcher.Invoke(() => new ViewCalibration(this)));
            this.SetIconResource("DICalibrationIcon");;

            EditCommand = new RelayCommand(a =>
            {
                var propertyEditorWindow = new PropertyEditorWindow(Config, PropertyEditorEditMode.Transactional) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                propertyEditorWindow.Submitted += (s, e) => Save();
                propertyEditorWindow.ShowDialog();
            }, a => AccessControl.Check(PermissionMode.Administrator));



            OpenPhyCameraMangerCommand = new RelayCommand(a => OpenPhyCameraManger(),a => AccessControl.Check(OpenPhyCameraManger) && PhyCamera !=null);
            DisplayLazy = new Lazy<DisplayCalibration>(() => new DisplayCalibration(this));
            AttachPhyCamera(PhyCamera);
            EditCalibrationCommand = new RelayCommand(a => EditCalibration());
            EditDisplayConfigCommand = new RelayCommand(_ => EditDisplayConfig());
            ReleaseLocalCalibrationCacheCommand = new RelayCommand(_ => LocalCalibrationCacheManagerWindow.OpenWindow());
        }

        [CommandDisplay("EditCalibration",Order =100, CategoryOrder = 1)]
        [Category("CalibrationCorrection")]
        [Description("CommandCameraCalibrationHint")]
        public RelayCommand EditCalibrationCommand { get; set; }

        [CommandDisplay("EditDisplayConfig", Order = -1, CategoryOrder = 2)]
        [Category("AcquisitionDisplay")]
        [Description("CommandDisplayConfigHint")]
        public RelayCommand EditDisplayConfigCommand { get; }

        [CommandDisplay("本地校正缓存管理", CategoryOrder = 1)]
        [Category("CalibrationCorrection")]
        [Description("CommandCalibrationCacheHint")]
        public RelayCommand ReleaseLocalCalibrationCacheCommand { get; }

        private void EditDisplayConfig()
        {
            new PropertyEditorWindow(DisplayConfig)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.ShowDialog();
        }

        public void EditCalibration()
        {
            if (PhyCamera == null)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.BeforeCalibrationSetupPhysicalCamera, "ColorVision");
                return;
            }
            var ITemplate = new TemplateCalibrationParam(PhyCamera);
            var windowTemplate = new TemplateEditorWindow(ITemplate) { Owner = Application.Current.GetActiveWindow() };
            windowTemplate.ShowDialog();
        }


        public void PhyCameraConfigChanged(object? sender, PhyCameras.Configs.ConfigPhyCamera e)
        {
            if (_isDisposed)
                return;

            Save();
        }

        private void AttachPhyCamera(PhyCamera? phyCamera)
        {
            if (_isDisposed && phyCamera != null)
                return;

            if (ReferenceEquals(_subscribedPhyCamera, phyCamera))
                return;

            if (_subscribedPhyCamera != null)
            {
                _subscribedPhyCamera.ConfigChanged -= PhyCameraConfigChanged;
                if (ReferenceEquals(_subscribedPhyCamera.DeviceCalibration, this))
                    _subscribedPhyCamera.DeviceCalibration = null;
            }

            _subscribedPhyCamera = phyCamera;
            if (_subscribedPhyCamera != null)
            {
                _subscribedPhyCamera.ConfigChanged += PhyCameraConfigChanged;
                _subscribedPhyCamera.DeviceCalibration = this;
            }
        }

        [CommandDisplay("ManagePhysicalCamera", CategoryOrder = 0)]
        [Category("DeviceConnection")]
        [Description("CommandPhysicalCameraHint")]
        public RelayCommand OpenPhyCameraMangerCommand { get; set; }

        [RequiresPermission(PermissionMode.Administrator)]
        public static void OpenPhyCameraManger()
        {
            new PhyCameraManagerWindow() { Owner = Application.Current.GetActiveWindow() }.Show();
        }

        public override void Save()
        {
            if (_isDisposed)
                return;

            base.Save();
            AttachPhyCamera(PhyCamera);
            _subscribedPhyCamera?.SetCalibration(this);
        }


        public override UserControl GetDeviceInfo() => new InfoCalibration(this);

        readonly Lazy<DisplayCalibration> DisplayLazy;

        public override UserControl GetDisplayControl() => DisplayLazy.Value;


        public override MQTTServiceBase? GetMQTTService()
        {
            return DService;
        }

        public override void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            if (DisplayLazy.IsValueCreated)
                DisplayLazy.Value.Dispose();

            if (_view.IsValueCreated)
            {
                DockViewManager.GetInstance().RemoveView(_view.Value);
                _view.Value.Dispose();
            }

            AttachPhyCamera(null);
            DService.Dispose();
            base.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
