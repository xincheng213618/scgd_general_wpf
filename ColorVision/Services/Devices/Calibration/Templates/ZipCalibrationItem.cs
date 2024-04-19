using cvColorVision;
using System.Collections.Generic;

namespace ColorVision.Services.Devices.Calibration.Templates
{
    public class ZipCalibrationGroup
    {
        public List<ZipCalibrationItem> ZipCalibrationItems { get; set; } = new List<ZipCalibrationItem>();


    }


    public class ZipCalibrationItem
    {
        public CalibrationType CalibrationType { get; set; }
        public string FileName { get; set; }
        public string Title { get; set; }
    }


}
