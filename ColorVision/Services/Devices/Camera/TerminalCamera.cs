using ColorVision.Common.MVVM;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Camera.Configs;
using ColorVision.Services.Extension;
using ColorVision.Services.Terminal;
using ColorVision.Settings;
using ColorVision.Themes;
using ColorVision.Common.Utilities;
using cvColorVision;
using Newtonsoft.Json;
using System;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Services.Devices.Camera
{
    public class TerminalCamera : TerminalService
    {
        public MQTTTerminalCamera MQTTTerminalCamera { get; set; }

        public TerminalCamera(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            OpenCreateWindowCommand = new RelayCommand(a => {
                CreateWindow createWindow = new CreateWindow(this);
                createWindow.Owner = Application.Current.GetActiveWindow();
                createWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                createWindow.ShowDialog();
            });

            MQTTTerminalCamera cameraService = new MQTTTerminalCamera(Config);
            MQTTServiceTerminalBase = cameraService;
            MQTTTerminalCamera = cameraService;
            this.SetIconResource("DrawingImageCamera");
        }
    }
}
