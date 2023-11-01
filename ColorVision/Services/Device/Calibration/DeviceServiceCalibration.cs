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
using ColorVision.Templates;
using System.Windows.Documents;

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


        public MsgRecord Calibration(CalibrationParam item,string FilePath,double R,double G,double B)
        {
            Dictionary<string, object> Params = new Dictionary<string, object>();
            MsgSend msg = new MsgSend
            {
                EventName = "Calibration",
                Params = Params
            };

            if (item.Color.Luminance.IsSelected)
            {
                Params.Add("CalibType", "Luminance");
                Params.Add("CalibTypeFileName", item.Color.Luminance.FilePath);
            }

            if (item.Color.LumOneColor.IsSelected)
            {
                Params.Add("CalibType", "LumOneColor");
                Params.Add("CalibTypeFileName", item.Color.LumOneColor.FilePath);
            }
            if (item.Color.LumFourColor.IsSelected)
            {
                Params.Add("CalibType", "LumFourColor");
                Params.Add("CalibTypeFileName", item.Color.LumFourColor.FilePath);
            }
            if (item.Color.LumMultiColor.IsSelected)
            {
                Params.Add("CalibType", "LumMultiColor");
                Params.Add("CalibTypeFileName", item.Color.LumMultiColor.FilePath);
            }

            List<Dictionary<string, object>> List = new List<Dictionary<string, object>>
            {
                item.NormalR.ToDictionary(),
                item.NormalG.ToDictionary(),
                item.NormalB.ToDictionary()
            };

            List[0].Add("EXPOSURE", R);
            List[1].Add("EXPOSURE", G);
            List[2].Add("EXPOSURE", B);



            Params.Add("List", List);


            Params.Add("szSrcFileName", FilePath);
            Params.Add("fname", 1);
            return PublishAsyncClient(msg);
        }
    }
}
