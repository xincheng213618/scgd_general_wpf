﻿using ColorVision.Services.Dao;
using ColorVision.Services.Templates;
using System.Collections.ObjectModel;

namespace ColorVision.Services.Devices.Calibration.Templates
{
    public interface ICalibrationService<T>
    {
        public ObservableCollection<T> VisualChildren { get; set; }
        public SysDeviceModel SysResourceModel { get; set; }
        public ObservableCollection<TemplateModel<CalibrationParam>> CalibrationParams { get; set; }
        public void AddChild(T t);
    }
}
