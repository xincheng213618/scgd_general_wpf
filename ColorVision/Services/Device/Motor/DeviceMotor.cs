﻿using ColorVision.MySql.DAO;
using ColorVision.Themes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Services.Device.Motor
{
    public class DeviceMotor : BaseDevice<ConfigMotor>
    {
        public DeviceServiceMotor DeviceService { get; set; }

        public DeviceMotor(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DeviceService = new DeviceServiceMotor(Config);

            if (Application.Current.TryFindResource("COMDrawingImage") is DrawingImage drawingImage)
                Icon = drawingImage;

            ThemeManager.Current.CurrentUIThemeChanged += (s) =>
            {
                if (Application.Current.TryFindResource("COMDrawingImage") is DrawingImage drawingImage)
                    Icon = drawingImage;
            };
        }

        public override UserControl GetDeviceControl() => new DeviceMotorControl(this);
        public override UserControl GetDeviceInfo() => new DeviceMotorControl(this, false);

        public override UserControl GetDisplayControl() => new DisplayMotorControl(this);


    }
}
