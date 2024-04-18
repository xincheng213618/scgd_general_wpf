using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Calibration.Templates;
using ColorVision.Services.PhyCameras.Configs;
using ColorVision.Services.Templates;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.PhyCameras
{
    public class PhyCamera : BaseResource
    {
        public ConfigPhyCamera Config { get; set; }

        public RelayCommand UploadCalibrationCommand { get; set; }
        public RelayCommand DisPlaySaveCommand { get; set; }

        public PhyCamera(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {


        }
    }
}
