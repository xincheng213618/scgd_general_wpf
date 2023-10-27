using ColorVision.Device.Camera;
using ColorVision.Device;
using ColorVision.Services.Device.Camera;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ColorVision.Services.Msg;
using System.Diagnostics;

namespace ColorVision.Services.Device.Calibration
{
    public class DeviceServiceCalibration : BaseDevService<ConfigCalibration>
    {
        public DeviceServiceCalibration(ConfigCalibration config) : base(config)
        {
            MsgReturnReceived += ProcessingReceived;
            DeviceStatus = DeviceStatus.UnInit;
        }

        private void ProcessingReceived(MsgReturn msg)
        {



        }
    }
}
