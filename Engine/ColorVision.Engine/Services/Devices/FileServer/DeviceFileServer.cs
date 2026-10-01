using ColorVision.Common.MVVM;
using ColorVision.ImageEditor;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.FileServer
{
    public class DeviceFileServer : DeviceService<ConfigFileServer>
    {
        private int _disposeState;
        public MQTTDeviceService<ConfigFileServer> DService { get; set; }

        public ImageView View { get; set; }
        public IDisplayConfigBase DisplayConfig => DisplayConfigManager.Instance.GetDisplayConfig<IDisplayConfigBase>(Config.Code);


        public DeviceFileServer(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTDeviceService<ConfigFileServer>(Config);
            View = new ImageView();

            EditCommand = new RelayCommand(a =>
            {
                var propertyEditorWindow = new PropertyEditorWindow(Config, PropertyEditorEditMode.Transactional) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                propertyEditorWindow.Submitted += (s, e) => Save();
                propertyEditorWindow.ShowDialog();
            },a => AccessControl.Check(PermissionMode.Administrator));
        }

        public override UserControl GetDeviceInfo() => new UserControl();

        public override MQTTServiceBase? GetMQTTService()
        {
            return DService;
        }

        public override void Dispose()
        {
            if (Interlocked.Exchange(ref _disposeState, 1) != 0) return;

            if (View.Dispatcher.CheckAccess())
                View.Dispose();
            else
                View.Dispatcher.Invoke(View.Dispose);
            DService.Dispose();
            base.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
