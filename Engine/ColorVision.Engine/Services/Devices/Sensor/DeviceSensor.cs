using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Sensor
{
    public partial class DeviceSensor : DeviceService<ConfigSensor>
    {
        public MQTTSensor DService { get; set; }
        public DisplaySensorConfig DisplayConfig { get; }
        public RelayCommand DiagnosticsCommand { get; }
        private SensorDiagnosticsWindow? sensorDiagnosticsWindow;

        public DeviceSensor(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DisplayConfig = DisplayConfigManager.Instance.GetDisplayConfig<DisplaySensorConfig>(Config.Code);
            InitializeLocalSensor();
            DService = new MQTTSensor(Config) { Device = this };
            DService.RefreshBackendStatus();
            DiagnosticsCommand = new RelayCommand(_ =>
            {
                if (sensorDiagnosticsWindow != null)
                {
                    if (sensorDiagnosticsWindow.WindowState == WindowState.Minimized) sensorDiagnosticsWindow.WindowState = WindowState.Normal;
                    sensorDiagnosticsWindow.Activate();
                    return;
                }
                sensorDiagnosticsWindow = new SensorDiagnosticsWindow(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                sensorDiagnosticsWindow.Closed += (_, _) => sensorDiagnosticsWindow = null;
                sensorDiagnosticsWindow.Show();
            });
            ContextMenu.Items.Add(new MenuItem { Header = EngineLocalization.Get("本地通信调试"), Command = DiagnosticsCommand });
            EditCommand = new RelayCommand(a =>
            {
                EditSensor window = new(this);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                if (window.ShowDialog() == true)
                {
                    ConfigHandler.GetInstance().Save<DisplayConfigManager>();
                    Save();
                }
            });

        }

        public override UserControl GetDeviceInfo() => new InfoSensor(this);
        public override UserControl GetDisplayControl() => new DisplaySensor(this);

        public override MQTTServiceBase? GetMQTTService()
        {
            return DService;
        }
    }
}
