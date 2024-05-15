using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Interfaces;
using ColorVision.Services.Dao;
using ColorVision.Services.Terminal;
using System.Windows;

namespace ColorVision.Services.Devices.Camera
{
    public class TerminalCamera : TerminalService
    {
        public MQTTTerminalCamera MQTTTerminalCamera { get; set; }

        public TerminalCamera(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            OpenCreateWindowCommand = new RelayCommand(a => {
                CreateWindow createWindow = new(this);
                createWindow.Owner = Application.Current.GetActiveWindow();
                createWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                createWindow.ShowDialog();
            });

            MQTTTerminalCamera cameraService = new(Config);
            MQTTServiceTerminalBase = cameraService;
            MQTTTerminalCamera = cameraService;
            this.SetIconResource("DrawingImageCamera");
        }
    }
}
