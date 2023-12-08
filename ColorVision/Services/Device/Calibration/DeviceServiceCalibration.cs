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
using System.ServiceModel.Channels;
using System.Windows;

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
            if (msg.Code == 0)
            {
                switch (msg.EventName)
                {
                    case "Calibration":

                        object obj = msg.Data;
                        Application.Current.Dispatcher.BeginInvoke(() => MessageBox.Show(Application.Current.MainWindow, obj.ToString()));
                        break;
                }
            }
            else
            {
                switch (msg.EventName)
                {
                    case "Calibration":
                        Application.Current.Dispatcher.BeginInvoke(() => MessageBox.Show(Application.Current.MainWindow, "校准失败"));
                        break;
                }
            }


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
                item.Normal.ToDictionary(),
            };

            List[0].Add("EXPOSURE", R);
            List[1].Add("EXPOSURE", G);
            List[2].Add("EXPOSURE", B);



            Params.Add("List", List);


            Params.Add("SrcFileName", FilePath);
            Params.Add("fname",DateTime.Now.ToString("yyyyMMddHHmmss") );
            return PublishAsyncClient(msg);
        }
    }
}
