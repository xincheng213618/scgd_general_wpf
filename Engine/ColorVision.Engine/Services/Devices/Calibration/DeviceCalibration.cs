using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.Engine.Services.Devices.Calibration.Views;
using ColorVision.Engine.Services.PhyCameras;
using log4net;
using System;
using System.Windows;
using System.Windows.Controls;
using ColorVision.UI.Authorizations;
using ColorVision.Database;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using System.ComponentModel;
using ColorVision.Themes.Controls;
using ColorVision.Engine.Extension;

namespace ColorVision.Engine.Services.Devices.Calibration
{
    public class DeviceCalibration : DeviceService<ConfigCalibration>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DeviceCalibration));

        public MQTTCalibration DService { get; set; }

        public DisplayCalibrationConfig DisplayConfig => DisplayConfigManager.Instance.GetDisplayConfig<DisplayCalibrationConfig>(Config.Code);

        public PhyCamera? PhyCamera { get => PhyCameraManager.GetInstance().GetPhyCamera(Config.CameraCode); }

        public ViewCalibration View{ get; set; }

        public DeviceCalibration(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTCalibration(Config);
            View = new ViewCalibration(this);
            View.View.Title = $"{Config.Code}";
            this.SetIconResource("DICalibrationIcon", View.View);;

            EditCommand = new RelayCommand(a =>
            {
                var propertyEditorWindow = new PropertyEditorWindow(Config, false) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                propertyEditorWindow.Submited += (s, e) => Save();
                propertyEditorWindow.ShowDialog();
            }, a => AccessControl.Check(PermissionMode.Administrator));



            OpenPhyCameraMangerCommand = new RelayCommand(a => OpenPhyCameraManger(),a => AccessControl.Check(OpenPhyCameraManger) && PhyCamera !=null);
            DisplayLazy = new Lazy<DisplayCalibrationControl>(() => new DisplayCalibrationControl(this));
            if (PhyCamera != null)
            {
                PhyCamera.ConfigChanged += PhyCameraConfigChanged;
                PhyCamera.DeviceCalibration = this;
            }
            EditCalibrationCommand = new RelayCommand(a => EditCalibration());
        }

        [CommandDisplay("EditCalibration",Order =100)]
        public RelayCommand EditCalibrationCommand { get; set; }

        public void EditCalibration()
        {
            if (PhyCamera == null)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.BeforeCalibrationSetupPhysicalCamera, "ColorVision");
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


        private PhyCamera lastPhyCamera;

        public void PhyCameraConfigChanged(object? sender, PhyCameras.Configs.ConfigPhyCamera e)
        {
            if (lastPhyCamera != null && sender is PhyCamera phyCamera && phyCamera != lastPhyCamera)
            {
                lastPhyCamera.ConfigChanged -= PhyCameraConfigChanged;
                lastPhyCamera = phyCamera;
                phyCamera.DeviceCalibration = this;
                lastPhyCamera.DeviceCalibration = null;
            }
            Save();
        }

        [CommandDisplay("ManagePhysicalCamera")]
        public RelayCommand OpenPhyCameraMangerCommand { get; set; }

        [RequiresPermission(PermissionMode.Administrator)]
        public static void OpenPhyCameraManger()
        {
            new PhyCameraManagerWindow() { Owner = Application.Current.GetActiveWindow() }.Show();
        }

        public override void Save()
        {
            base.Save();
            if (PhyCamera != null)
                PhyCamera.SetCalibration(this);
        }


        public override UserControl GetDeviceInfo() => new InfoCalibration(this);

        readonly Lazy<DisplayCalibrationControl> DisplayLazy;

        public override UserControl GetDisplayControl() => DisplayLazy.Value;


        public override MQTTServiceBase? GetMQTTService()
        {
            return DService;
        }
    }
}
