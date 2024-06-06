using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Util.Interfaces;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Terminal;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Camera
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
