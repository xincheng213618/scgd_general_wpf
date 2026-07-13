#pragma warning disable CS8602
using ColorVision.Database;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using MQTTMessageLib.FileServer;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Algorithm
{

    public class MQTTAlgorithm : MQTTDeviceService<ConfigAlgorithm>
    {
        public DeviceAlgorithm Device { get; set; }


        public MQTTAlgorithm(DeviceAlgorithm device, ConfigAlgorithm Config) : base(Config)
        {
            Device = device;
            MsgReturnReceived += MQTTAlgorithm_MsgReturnReceived;   
            DeviceStatus = DeviceStatusType.Unknown;
        }


        private void MQTTAlgorithm_MsgReturnReceived(MsgReturn msg)
        {
            if (msg.DeviceCode != Config.Code) return;
            switch (msg.EventName)
            {
                default:
                    // 判断 msg.Data 不为 null 并且包含 MasterId 属性
                    if (msg.Data != null && msg.Data.MasterId != null && msg.Data.MasterId > 0)
                    {
                        int masterId = msg.Data.MasterId;
                        AlgResultMasterModel model = AlgResultMasterDao.Instance.GetById(masterId);
                        if (model != null)
                        {
                            log.Debug($"FileUrl：{model.ImgFile}");
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                Device.View.AddAlgResultMasterModel(model);
                            });
                        }
                        else
                        {
                            log.Debug($"GetImgResult By Id is null: {masterId}");
                        }
                    }


                    break;
            }
        }

        public MsgRecord CacheClear()
        {
            return PublishAsyncClient(new MsgSend { EventName = "" });
        }


    }
}
