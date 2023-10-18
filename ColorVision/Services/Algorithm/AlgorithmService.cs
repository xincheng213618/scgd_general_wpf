#pragma warning disable CS8602  

using ColorVision.MQTT;
using ColorVision.Services.Msg;
using ColorVision.Template;
using ColorVision.Template.Algorithm;
using cvColorVision;
using Newtonsoft.Json;
using NPOI.SS.Formula.Functions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Windows;

namespace ColorVision.Device.Algorithm
{
    public class AlgorithmService : BaseDevService<AlgorithmConfig>
    {
        public event DeviceStatusChangedHandler DeviceStatusChanged;

        public DeviceStatus DeviceStatus { get => _DeviceStatus; set { if (value == _DeviceStatus) return;  _DeviceStatus = value; Application.Current.Dispatcher.Invoke(() => DeviceStatusChanged?.Invoke(value)); NotifyPropertyChanged(); } }
        private DeviceStatus _DeviceStatus;

        public static Dictionary<string, ObservableCollection<string>> ServicesDevices { get; set; } = new Dictionary<string, ObservableCollection<string>>();

        public string DeviceID { get => Config.ID; }

        public AlgorithmService(AlgorithmConfig Config) : base(Config)
        {
            MsgReturnReceived += MQTTCamera_MsgReturnChanged;
            DeviceStatus = DeviceStatus.UnInit;
        }

        private void MQTTCamera_MsgReturnChanged(MsgReturn msg)
        {
            IsRun = false;
            switch (msg.EventName)
            {
                case "CM_GetAllSnID":
                    try
                    {
                        SnID = msg.Data.SnID[0];
                    }
                    catch (Exception ex)
                    {
                        if (log.IsErrorEnabled)
                            log.Error(ex);
                    }
                    return;
            }


            //信息在这里添加一次过滤，让信息只能在对应的相机上显示,同时如果ID为空的话，就默认是服务端的信息，不进行过滤，这里后续在进行优化
            if (Config.ID != null && msg.SnID != Config.ID)
            {
                return;
            }

            if (msg.Code == 0)
            {

                switch (msg.EventName)
                {
                    case "Init":
                        ServiceID = msg.ServiceID;
                        DeviceStatus = DeviceStatus.Init;
                        break;
                    case "UnInit":
                        DeviceStatus = DeviceStatus.UnInit;
                        break;
                    case "SetParam":
                        break;
                    case "Close":
                        DeviceStatus = DeviceStatus.Closed;
                        break;
                    case "Open":
                        DeviceStatus = DeviceStatus.Opened;
                        break;
                    case "GetData":
                        DeviceStatus = DeviceStatus.Opened;
                        break;
                    case "SaveLicense":
                        break;
                    default:
                        MessageBox.Show($"未定义{msg.EventName}");
                        break;
                }
            }
            else
            {
                MessageBox.Show("返回失败" + Environment.NewLine + msg);
                switch (msg.EventName)
                {
                    case "GetData":
                        break;
                    case "Close":
                        DeviceStatus = DeviceStatus.UnInit;
                        break;
                    case "Open":
                        DeviceStatus = DeviceStatus.UnInit;
                        break;
                    case "Init":
                        DeviceStatus = DeviceStatus.UnInit;
                        break;
                    case "UnInit":
                        DeviceStatus = DeviceStatus.UnInit;
                        break;
                    case "Calibrations":
                        break;
                    default:
                        break;
                }
            }
        }

        public bool IsRun { get; set; }
        public MsgRecord Init()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Init",
                Params = new Dictionary<string, object>() {{ "SnID", SnID } }
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord UnInit()
        {
            MsgSend msg = new MsgSend  {  EventName = "UnInit"};
            return PublishAsyncClient(msg);
        }


        public MsgRecord GetData(int pid,int Batchid)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "GetData",
                Params = new Dictionary<string, object>() { { "SnID", SnID }, { "nPid", pid }, { "nBatch", Batchid } }
            };
            //Params = new Dictionary<string, object>() { { "SnID", SnID }, { "nPid", pid }, { "nBatch", Batchid },{ "eCalibType", (int)eCalibType }, { "szFileNameX", "X.tif " }, { "szFileNameY", "Y.tif " }, { "szFileNameZ", "Z.tif " } }
            return PublishAsyncClient(msg);
        }

        public MsgRecord GetData(int pid, int Batchid,string fileName,string fileName1,string fileName2)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "GetData",
                Params = new Dictionary<string, object>() { { "SnID", SnID }, { "nPid", pid }, { "nBatch", Batchid } }
            };
            msg.Params.Add.Add("file_data", ToJsonFileList(ImageChannelType.Gray_Y,fileName, fileName1, fileName2));
            return PublishAsyncClient(msg);
        }

        public MsgRecord FOV(int pid, int Batchid)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "FOV",
                Params = new Dictionary<string, object>() { { "SnID", SnID }, { "nPid", pid }, { "nBatch", Batchid } }
            };
            //Params = new Dictionary<string, object>() { { "SnID", SnID }, { "nPid", pid }, { "nBatch", Batchid },{ "eCalibType", (int)eCalibType }, { "szFileNameX", "X.tif " }, { "szFileNameY", "Y.tif " }, { "szFileNameZ", "Z.tif " } }
            return PublishAsyncClient(msg);
        }

        public MsgRecord FOV(int pid, string fileName, FOVParam fOVParam)
        {  
            MsgSend msg = new MsgSend
            {
                EventName = "FOV",
                Params = new Dictionary<string, object>() { { "SnID", SnID }, { "nPid", pid }, { "nBatch", 0 } }
            };

            msg.Params.Add("radio", fOVParam.Radio);
            msg.Params.Add("cameraDegrees", fOVParam.CameraDegrees);
            msg.Params.Add("DFovDist", fOVParam.DFovDist);
            msg.Params.Add("FovPattern", (int)fOVParam.FovPattern);
            msg.Params.Add("FovType", (int)fOVParam.FovType);
            msg.Params.Add("file_data", ToJsonFileList(ImageChannelType.Gray_Y, fileName));
            return PublishAsyncClient(msg);
        }




        public MsgRecord GetAllSnID() => PublishAsyncClient(new MsgSend { EventName = "CM_GetAllSnID" });


        public MsgRecord SetLicense(string md5, string FileData)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "SaveLicense",
                Params = new Dictionary<string, object>() { { "FileName", md5 }, { "FileData", FileData }, { "eType", 0 }}
            };
            return PublishAsyncClient(msg); 
        }

        //public MsgRecord GetData(int pid, int Batchid)
        //{
        //    MsgSend msg = new MsgSend 
        //    { 
        //        EventName = "GetData",
        //        Params = new Dictionary<string, object>() { { "SnID", SnID }, { "nPid", pid }, { "nBatch", Batchid } }
        //    };
        //    return PublishAsyncClient(msg);
        //}



        public MsgRecord MTF(int pid, int Batchid,int modid)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "MTF",
                Params = new Dictionary<string, object>() { { "SnID", SnID }, { "nPid", pid }, { "nBatch", Batchid }, { "nMod", modid } }
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord MTF(int pid, string FileName,MTFParam mTFParam)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "MTF",
                Params = new Dictionary<string, object>() { { "SnID", SnID }, { "nPid", pid }, { "nBatch", 0 }, }
            };

            msg.Params.Add("eEvaFunc", (int)mTFParam.eEvaFunc);
            msg.Params.Add("dx", (int)mTFParam.dx);
            msg.Params.Add("dy", (int)mTFParam.dy);
            msg.Params.Add("ksize", (int)mTFParam.ksize);
            msg.Params.Add("dRatio", (int)mTFParam.MTF_dRatio);

            msg.Params.Add("file_data", ToJsonFileList(ImageChannelType.Gray_Y, FileName));  
            return PublishAsyncClient(msg);
        }

        private static string ToJsonFileList(ImageChannelType imageChannelType, params string[] FileNames)
        {
            Dictionary<string, object> file_data  =new Dictionary<string, object>();

            file_data.Add("eCalibType", CalibrationType.DarkNoise);

            List<Dictionary<string, object>> keyValuePairs = new List<Dictionary<string, object>>();
            foreach (var item in FileNames)
            {
                Dictionary<string, object> keyValuePairs1 = new Dictionary<string, object>();
                keyValuePairs1.Add("Type", imageChannelType);
                keyValuePairs1.Add("filename", item);
                keyValuePairs.Add(keyValuePairs1);
            }
            file_data.Add("list", keyValuePairs);
            return JsonConvert.SerializeObject(file_data);
        }



        public MsgRecord SFR(int pid, int Batchid, int modid)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "SFR",
                Params = new Dictionary<string, object>() { { "SnID", SnID }, { "nPid", pid }, { "nBatch", Batchid }, { "nMod", modid } }
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord SFR(int pid,  string FileName,SFRParam sFRParam )
        {
            MsgSend msg = new MsgSend
            {
                EventName = "SFR",
                Params = new Dictionary<string, object>() { { "SnID", SnID }, { "nPid", pid }, { "nBatch", 0 } }
            };
            msg.Params.Add("x", sFRParam.ROI.x);
            msg.Params.Add("y", sFRParam.ROI.y);
            msg.Params.Add("cx", sFRParam.ROI.cx);
            msg.Params.Add("cy", sFRParam.ROI.cy);
            msg.Params.Add("gamma", sFRParam.SFR_gamma);

            msg.Params.Add("file_data", ToJsonFileList(ImageChannelType.Gray_Y, FileName));
            return PublishAsyncClient(msg);
        }

        public MsgRecord Ghost(int pid, int Batchid, int modid)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Ghost",
                Params = new Dictionary<string, object>() { { "SnID", SnID }, { "nPid", pid }, { "nBatch", Batchid }, { "nMod", modid } }
            };
            return PublishAsyncClient(msg);
        }


        public MsgRecord Ghost(int pid,string FileName, GhostParam ghostParam)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Ghost",
                Params = new Dictionary<string, object>() { { "SnID", SnID }, { "nPid", pid }, { "nBatch", 0 } }
            };
            msg.Params.Add("cols", ghostParam.Ghost_cols);
            msg.Params.Add("rows", ghostParam.Ghost_rows);

            msg.Params.Add("radius", ghostParam.Ghost_radius);
            msg.Params.Add("ratioH", ghostParam.Ghost_ratioH);
            msg.Params.Add("ratioL", ghostParam.Ghost_ratioL);

            msg.Params.Add("file_data", ToJsonFileList(ImageChannelType.CIE_Y, FileName));
            return PublishAsyncClient(msg);
        }



        public MsgRecord Distortion(int pid, int Batchid)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Distortion",
                Params = new Dictionary<string, object>() { { "SnID", SnID }, { "nPid", pid }, { "nBatch", Batchid } }
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord Distortion(int pid, string FileName, DistortionParam distortionParam)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Distortion",
                Params = new Dictionary<string, object>() { { "SnID", SnID }, { "nPid", pid }, { "nBatch", 0 } }
            };

            msg.Params.Add("filterByColor", distortionParam.filterByColor);
            msg.Params.Add("blobColor", distortionParam.blobColor);
            msg.Params.Add("minThreshold", distortionParam.minThreshold);
            msg.Params.Add("thresholdStep", distortionParam.thresholdStep);
            msg.Params.Add("ifDEBUG", distortionParam.ifDEBUG);
            msg.Params.Add("darkRatio", distortionParam.darkRatio);
            msg.Params.Add("contrastRatio", distortionParam.contrastRatio);
            msg.Params.Add("bgRadius", distortionParam.bgRadius);
            msg.Params.Add("filterByArea", distortionParam.filterByArea);
            msg.Params.Add("minArea", distortionParam.minArea);
            msg.Params.Add("maxArea", distortionParam.maxArea);
            msg.Params.Add("minRepeatability", distortionParam.minRepeatability);
            msg.Params.Add("filterByCircularity", distortionParam.filterByCircularity);
            msg.Params.Add("minCircularity", distortionParam.minCircularity);
            msg.Params.Add("maxCircularity", distortionParam.maxCircularity);
            msg.Params.Add("filterByConvexity", distortionParam.filterByConvexity);
            msg.Params.Add("minConvexity", distortionParam.minConvexity);
            msg.Params.Add("maxConvexity", distortionParam.maxConvexity);
            msg.Params.Add("filterByInertia", distortionParam.filterByInertia);
            msg.Params.Add("minInertiaRatio", distortionParam.minInertiaRatio);
            msg.Params.Add("maxInertiaRatio", distortionParam.maxInertiaRatio);

            msg.Params.Add("x", distortionParam.ROI.x);
            msg.Params.Add("y", distortionParam.ROI.y);
            msg.Params.Add("cx", distortionParam.ROI.cx);
            msg.Params.Add("cy", distortionParam.ROI.cy);
            msg.Params.Add("file_data", ToJsonFileList(ImageChannelType.Gray_Y, FileName));

            return PublishAsyncClient(msg);
        }



        public MsgRecord FocusPoints(int pid, int Batchid)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "FocusPoints",
                Params = new Dictionary<string, object>() { { "SnID", SnID }, { "nPid", pid }, { "nBatch", Batchid } }
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord LedCheck(int pid, int Batchid)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "LedCheck",
                Params = new Dictionary<string, object>() { { "SnID", SnID }, { "nPid", pid }, { "nBatch", Batchid } }
            };
            return PublishAsyncClient(msg);
        }


        public MsgRecord Close()
        {
            MsgSend msg = new MsgSend {  EventName = "Close" };
            return PublishAsyncClient(msg);
        }

    }



}
