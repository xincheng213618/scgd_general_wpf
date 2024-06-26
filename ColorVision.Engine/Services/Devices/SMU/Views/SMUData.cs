﻿using ColorVision.Common.MVVM;

namespace ColorVision.Engine.Services.Devices.SMU.Views
{
    public class SMUData : ViewModelBase
    {
        /// <summary>
        /// 电压
        /// </summary>
        public double Voltage { get; set; }
        /// <summary>
        /// 电流
        /// </summary>
        public double Current { get; set; }
    }
}
