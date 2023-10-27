#pragma warning disable CS8602  

using ColorVision.Device.SMU;
using ColorVision.Lincense;
using ColorVision.Services;
using ColorVision.Services.Msg;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Device.Camera
{
    public class CameraService : BaseService<BaseServiceConfig>
    {
        public CameraService(BaseServiceConfig Config) :base(Config)
        {
            Devices = new List<CameraDeviceService>();
            Connected += (s, e) =>
            {
                GetAllDevice();
            };
            GetAllDevice();
            MsgReturnReceived += (msg) => Application.Current.Dispatcher.Invoke(()=> MQTTCamera_MsgReturnChanged(msg));
        }



        private void MQTTCamera_MsgReturnChanged(MsgReturn msg)
        {
            switch (msg.EventName)
            {
                case "CM_GetAllSnID":
                    try
                    {

                        if (msg.Data == null)
                            return;

                       JArray SnIDs = msg.Data.SnID;
                        if (SnIDs != null)
                        {
                            DevicesSN = new ObservableCollection<string>();
                            for (int i = 0; i < SnIDs.Count; i++)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    DevicesSN.Add(SnIDs[i].ToString());
                                });
                            }
                        }

                        JArray MD5IDs = msg.Data.MD5ID;

                        if (SnIDs == null || MD5IDs == null)
                        {
                            return;
                        }

                        for (int i = 0; i < MD5IDs.Count; i++)
                        {

                            if (DevicesSNMD5.ContainsKey(SnIDs[i].ToString()))
                            {
                                DevicesSNMD5[SnIDs[i].ToString()]= MD5IDs[i].ToString();

                            }
                            else
                            {
                                DevicesSNMD5.Add(SnIDs[i].ToString(), MD5IDs[i].ToString());
                            }

                            LicenseManager.GetInstance().AddLicense(new LicenseConfig() { Name = SnIDs[i].ToString(), Sn = MD5IDs[i].ToString(), IsCanImport = true });
                        }
                    }
                    catch (Exception ex)
                    {
                        if (log.IsErrorEnabled)
                            log.Error(ex);
                    }

                    return;
            }
        }
        public void GetAllDevice()
        {
            PublishAsyncClient(new MsgSend { EventName = "CM_GetAllSnID" });
        }


    }



}
