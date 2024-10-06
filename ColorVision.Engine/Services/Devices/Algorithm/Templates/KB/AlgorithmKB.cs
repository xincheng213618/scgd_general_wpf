using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.Msg;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using MQTTMessageLib;
using MQTTMessageLib.Algorithm;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.KB
{
    public class AlgorithmKB : ViewModelBase, IDisplayAlgorithm
    {
        public string Name { get; set; } = "KB";

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }


        public AlgorithmKB(DeviceAlgorithm deviceAlgorithm)
        {
            Device = deviceAlgorithm;
        }

        public UserControl GetUserControl()
        {
            UserControl ??= new DisplayKB(this);
            return UserControl;
        }
        public UserControl UserControl { get; set; }

    }
}
