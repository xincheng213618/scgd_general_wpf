using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Calibration.Views;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
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
        private volatile bool _isDisposed;
        private PhyCamera? _subscribedPhyCamera;

        public MQTTCalibration DService { get; set; }
        internal bool IsDisposed => _isDisposed;

        public DisplayCalibrationConfig DisplayConfig => DisplayConfigManager.Instance.GetDisplayConfig<DisplayCalibrationConfig>(Config.Code);

        public PhyCamera? PhyCamera { get => PhyCameraManager.GetInstance().GetPhyCamera(Config.CameraCode); }

        private readonly DockViewRegistration _view;
        internal DockViewRegistration ViewRegistration => _view;
        internal ViewCalibration? ExistingView => _view.Current as ViewCalibration;
        internal ViewCalibration ViewShell => (ViewCalibration)_view.GetOrCreate();
        public ViewCalibration View
        {
            get
            {
                ViewCalibration view = ViewShell;
                view.EnsureInitialized();
                return view;
            }
        }

        public DeviceCalibration(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTCalibration(this);
            _view = new DockViewRegistration(() => new ViewCalibration(this, deferInitialization: true), Config.Name, DisPlayManager.IsControlVisible(Config.Code, Config.Name))
                { PersistenceKey = Config.Code };
            EditCommand = new RelayCommand(a =>
            {
                if (!CanConfigure) return;
                var propertyEditorWindow = new PropertyEditorWindow(Config, PropertyEditorEditMode.Transactional) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                var target = ProtectConfigurationEditor(propertyEditorWindow);
                propertyEditorWindow.Submitted += (_, _) => TryCommitConfiguration(propertyEditorWindow, expectedTarget: target);
                propertyEditorWindow.ShowDialog();
            }, a => CanConfigure);



            OpenPhyCameraMangerCommand = new RelayCommand(a => OpenPhyCameraManger(),a => AccessControl.Evaluate("physical-camera.manage", PermissionMode.Administrator).Allowed && PhyCamera !=null);
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

        [CommandDisplay("EditDisplayConfig", Order = -1, CategoryOrder = 0)]
        [Category("DeviceConnection")]
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
                _subscribedPhyCamera.Config.PropertyChanged -= PhysicalCameraIdentityChanged;
                if (ReferenceEquals(_subscribedPhyCamera.DeviceCalibration, this))
                    _subscribedPhyCamera.DeviceCalibration = null;
            }

            _subscribedPhyCamera = phyCamera;
            if (_subscribedPhyCamera != null)
            {
                PhysicalCameraIdentity.RecoverFromLogical(_subscribedPhyCamera, Config.CameraCode, Config.CameraID);
                _subscribedPhyCamera.ConfigChanged += PhyCameraConfigChanged;
                _subscribedPhyCamera.Config.PropertyChanged += PhysicalCameraIdentityChanged;
                _subscribedPhyCamera.DeviceCalibration = this;
                Config.CameraID = _subscribedPhyCamera.Config.CameraID?.Trim() ?? string.Empty;
            }
        }

        private void PhysicalCameraIdentityChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!_isDisposed && ReferenceEquals(sender, _subscribedPhyCamera?.Config) && e.PropertyName == nameof(ConfigCalibration.CameraID))
                Config.CameraID = _subscribedPhyCamera.Config.CameraID?.Trim() ?? string.Empty;
        }

        [CommandDisplay("ManagePhysicalCamera", CategoryOrder = 0)]
        [Category("DeviceConnection")]
        [Description("CommandPhysicalCameraHint")]
        public RelayCommand OpenPhyCameraMangerCommand { get; set; }

        [RequiresPermission(PermissionMode.Administrator, ActionId = "physical-camera.manage")]
        public static void OpenPhyCameraManger()
        {
            AccessControl.Demand("physical-camera.manage", PermissionMode.Administrator);
            new PhyCameraManagerWindow() { Owner = Application.Current.GetActiveWindow() }.Show();
        }

        public override void Save()
        {
            if (_isDisposed)
                return;

            PhyCamera? selectedCamera = null;
            (SysResourceModel? Resource, string? Value) selectedSource = default;
            string? selectedCode = null;
            SaveWithConfigurationTarget(prepare: target =>
            {
                selectedCode = Config.CameraCode;
                selectedCamera = PhyCamera;
                if (selectedCamera == null) return;
                selectedSource = (selectedCamera.SysResourceModel, selectedCamera.SysResourceModel.Value);
                var physicalConfig = selectedCamera.ReadOwnedPhysicalConfiguration(this, target)
                    ?? throw new DeviceConfigurationSaveException(EngineLocalization.Get("关联的物理相机已变更，请重新选择设备。"));

                string? recoveredId = ReferenceEquals(_subscribedPhyCamera, selectedCamera) ? null
                    : PhysicalCameraIdentity.FindRecoverableId(selectedCamera.Code, physicalConfig.CameraID, Config.CameraCode, Config.CameraID);
                Config.CameraID = recoveredId ?? physicalConfig.CameraID;
            }, apply: target =>
            {
                if (!target.IsCurrent(this) || !string.Equals(Config.CameraCode, selectedCode, StringComparison.Ordinal)
                    || !ReferenceEquals(selectedCamera?.SysResourceModel, selectedSource.Resource)
                    || !string.Equals(selectedCamera?.SysResourceModel.Value, selectedSource.Value, StringComparison.Ordinal)) return;
                string savedCameraId = Config.CameraID;
                AttachPhyCamera(selectedCamera);
                if (!string.Equals(Config.CameraID, savedCameraId, StringComparison.Ordinal)) Config.CameraID = savedCameraId;
                selectedCamera?.SetCalibration(this, target);
            }, applyAfterNotification: true);
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

            _view.Dispose();

            AttachPhyCamera(null);
            DService.Dispose();
            base.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
