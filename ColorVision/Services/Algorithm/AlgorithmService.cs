#pragma warning disable CS8602  

using ColorVision.Services.Algorithm.Templates;
using ColorVision.Services.Device;
using ColorVision.Services.Msg;
using cvColorVision;
using MQTTMessageLib.Algorithm;
using MQTTMessageLib.FileServer;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Services.Algorithm
{
    public class AlgorithmService : BaseDevService<ConfigAlgorithm>
    {
        public static Dictionary<string, ObservableCollection<string>> ServicesDevices { get; set; } = new Dictionary<string, ObservableCollection<string>>();

        public string DeviceID { get => Config.ID; }

        public event MessageRecvHandler OnMessageRecved;


        public AlgorithmService(ConfigAlgorithm Config) : base(Config)
        {
            MsgReturnReceived += MQTTCamera_MsgReturnChanged;
            DeviceStatus = DeviceStatus.UnInit;
            GetAllSnID();
            Connected +=(s,e)=> GetAllSnID();
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
                        Init();
                    }
                    catch (Exception ex)
                    {
                        if (log.IsErrorEnabled)
                            log.Error(ex);
                    }
                    return;
            }

            if (msg.Code == 0)
            {

                switch (msg.EventName)
                {
                    case "Init":
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
                    case MQTTAlgorithmEventEnum.Event_POI_GetData:
                        OnMessageRecved?.Invoke(this, new MessageRecvEventArgs(msg.EventName, msg.SerialNumber, msg.Code, msg.Data));
                        DeviceStatus = DeviceStatus.Opened;
                        break;
                    case "SaveLicense":
                        break;
                    case MQTTFileServerEventEnum.Event_File_Download:
                    //    break;
                    case MQTTFileServerEventEnum.Event_File_Upload:
                    case MQTTFileServerEventEnum.Event_File_List_All:
                        OnMessageRecved?.Invoke(this, new MessageRecvEventArgs(msg.EventName, msg.SerialNumber, msg.Code, msg.Data));
                        break;
                    case "MTF":
                        Application.Current.Dispatcher.BeginInvoke(() => MessageBox.Show(Application.Current.MainWindow, $"{msg.EventName}执行成功", "ColorVision"));
                        break;
                    default:
                        Application.Current.Dispatcher.BeginInvoke(() => MessageBox.Show(Application.Current.MainWindow, $"{msg.EventName}执行成功", "ColorVision"));
                        break;
                }
            }
            else
            {
                switch (msg.EventName)
                {
                    case "GetData":
                        OnMessageRecved?.Invoke(this, new MessageRecvEventArgs(msg.EventName, msg.SerialNumber, msg.Code, msg.Data));
                        DeviceStatus = DeviceStatus.Opened;
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
                        OnMessageRecved?.Invoke(this, new MessageRecvEventArgs(msg.EventName, msg.SerialNumber, msg.Code, msg.Data));
                        DeviceStatus = DeviceStatus.Opened;
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
                Params = new Dictionary<string, object>() { { "SnID", SnID } , {"CodeID",Config.Code } }
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord UnInit()
        {
            MsgSend msg = new MsgSend  {  EventName = "UnInit"};
            return PublishAsyncClient(msg);
        }

        public void GetCIEFiles()
        {
            MsgSend msg = new MsgSend
            {
                EventName = MQTTFileServerEventEnum.Event_File_List_All,
                Params = new Dictionary<string, object> { { "FileExtType", FileExtType.CIE } }
            };
            PublishAsyncClient(msg);
        }

        public MsgRecord GetData(int pid,string fileName,string tempName,string serialNumber)
        {
            string sn = null;
            if(string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;
            MsgSend msg = new MsgSend
            {
                EventName = "GetData",
                SerialNumber = sn,
                Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "TemplateId", pid }, { "TemplateName", tempName }, { "nBatchID", -1 } }
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord FOV( string fileName, FOVParam fOVParam)
        {  
            MsgSend msg = new MsgSend
            {
                EventName = "FOV",
                Params = new Dictionary<string, object>() { { "nBatchID", -1 } }
            };

            msg.Params.Add("radio", fOVParam.Radio);
            msg.Params.Add("cameraDegrees", fOVParam.CameraDegrees);
            msg.Params.Add("dFovDist", fOVParam.DFovDist);
            msg.Params.Add("fovPattern", (int)fOVParam.FovPattern);
            msg.Params.Add("fovType", (int)fOVParam.FovType);
            msg.Params.Add("thresholdValus", (int)fOVParam.ThresholdValus);
            msg.Params.Add("x_c", fOVParam.Xc);
            msg.Params.Add("y_c", fOVParam.Yc);
            msg.Params.Add("x_p", fOVParam.Xp);
            msg.Params.Add("y_p", fOVParam.Yp);
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


        public MsgRecord MTF(string FileName,MTFParam mTFParam)
        {
            string SerialNumber = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            var model = ServiceManager.GetInstance().BatchSave(SerialNumber);

            MsgSend msg = new MsgSend
            {
                EventName = "MTF",
                SerialNumber = SerialNumber,
                Params = new Dictionary<string, object>() { { "SnID", SnID }, { "nBatchID", -1 }, }
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

            file_data.Add("eCalibType", cvColorVision.CalibrationType.DarkNoise);

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

        public MsgRecord SFR(int pid, string FileName,SFRParam sFRParam )
        {
            string SerialNumber = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            var model = ServiceManager.GetInstance().BatchSave(SerialNumber);

            MsgSend msg = new MsgSend
            {
                EventName = "SFR",
                DeviceCode = Config.Code,
                SerialNumber = SerialNumber,
                Params = new Dictionary<string, object>() {{ "nPid", pid }, { "nBatchID", -1 } }
            };

            msg.Params.Add("x", sFRParam.ROI.x);
            msg.Params.Add("y", sFRParam.ROI.y);
            msg.Params.Add("cx", sFRParam.ROI.cx);
            msg.Params.Add("cy", sFRParam.ROI.cy);
            msg.Params.Add("gamma", sFRParam.Gamma);

            msg.Params.Add("file_data", ToJsonFileList(ImageChannelType.Gray_Y, FileName));
            return PublishAsyncClient(msg);
        }


        public MsgRecord Ghost(string FileName, GhostParam ghostParam)
        {
            string SerialNumber = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            var model = ServiceManager.GetInstance().BatchSave(SerialNumber);
            MsgSend msg = new MsgSend
            {
                EventName = "Ghost",
                DeviceCode = Config.Code,
                SerialNumber = SerialNumber,
                Params = new Dictionary<string, object>() {{ "nBatchID", -1 } }
            };
            msg.Params.Add("bSQL", 1);
            msg.Params.Add("MName", "default1");


            msg.Params.Add("LedNums_X", ghostParam.Ghost_cols);
            msg.Params.Add("LedNums_Y", ghostParam.Ghost_rows);

            msg.Params.Add("radius", ghostParam.Ghost_radius);
            msg.Params.Add("ratioH", ghostParam.Ghost_ratioH);
            msg.Params.Add("ratioL", ghostParam.Ghost_ratioL);

            msg.Params.Add("file_data", ToJsonFileList(ImageChannelType.CIE_Y, FileName));
            return PublishAsyncClient(msg);
        }

        public MsgRecord Distortion(string FileName, DistortionParam distortionParam)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Distortion",
                Params = new Dictionary<string, object>() { { "nBatchID", -1 } }
            };
            var Blob_Threshold_Params = new Dictionary<string, object>() { };

            Blob_Threshold_Params.Add("filterByColor", distortionParam.filterByColor);
            Blob_Threshold_Params.Add("blobColor", distortionParam.blobColor);
            Blob_Threshold_Params.Add("minThreshold", distortionParam.minThreshold);
            Blob_Threshold_Params.Add("maxThreshold", distortionParam.maxThreshold);   
            Blob_Threshold_Params.Add("thresholdStep", distortionParam.thresholdStep);
            Blob_Threshold_Params.Add("ifDEBUG", distortionParam.ifDEBUG);
            Blob_Threshold_Params.Add("darkRatio", distortionParam.darkRatio);
            Blob_Threshold_Params.Add("contrastRatio", distortionParam.contrastRatio);
            Blob_Threshold_Params.Add("bgRadius", distortionParam.bgRadius);
            Blob_Threshold_Params.Add("minDistBetweenBlobs", distortionParam.minDistBetweenBlobs);
            Blob_Threshold_Params.Add("filterByArea", distortionParam.filterByArea);
            Blob_Threshold_Params.Add("minArea", distortionParam.minArea);
            Blob_Threshold_Params.Add("maxArea", distortionParam.maxArea);
            Blob_Threshold_Params.Add("minRepeatability", distortionParam.minRepeatability);
            Blob_Threshold_Params.Add("filterByCircularity", distortionParam.filterByCircularity);
            Blob_Threshold_Params.Add("minCircularity", distortionParam.minCircularity);
            Blob_Threshold_Params.Add("maxCircularity", distortionParam.maxCircularity);
            Blob_Threshold_Params.Add("filterByConvexity", distortionParam.filterByConvexity);
            Blob_Threshold_Params.Add("minConvexity", distortionParam.minConvexity);
            Blob_Threshold_Params.Add("maxConvexity", distortionParam.maxConvexity);
            Blob_Threshold_Params.Add("filterByInertia", distortionParam.filterByInertia);
            Blob_Threshold_Params.Add("minInertiaRatio", distortionParam.minInertiaRatio);
            Blob_Threshold_Params.Add("maxInertiaRatio", distortionParam.maxInertiaRatio);

            msg.Params.Add("Blob_Threshold_Params", Blob_Threshold_Params);

            msg.Params.Add("filterByColor", distortionParam.filterByColor);
            msg.Params.Add("blobColor", distortionParam.blobColor);
            msg.Params.Add("minThreshold", distortionParam.minThreshold);
            msg.Params.Add("maxThreshold", distortionParam.maxThreshold);
            msg.Params.Add("thresholdStep", distortionParam.thresholdStep);
            msg.Params.Add("ifDEBUG", distortionParam.ifDEBUG);
            msg.Params.Add("darkRatio", distortionParam.darkRatio);
            msg.Params.Add("contrastRatio", distortionParam.contrastRatio);
            msg.Params.Add("bgRadius", distortionParam.bgRadius);
            msg.Params.Add("minDistBetweenBlobs", distortionParam.minDistBetweenBlobs);
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

            var ISIZE = new Dictionary<string, object>() {};
            ISIZE.Add("cx", distortionParam.Width);
            ISIZE.Add("cy", distortionParam.Height);
            msg.Params.Add("ISIZE", ISIZE);



            msg.Params.Add("cx", distortionParam.Width);
            msg.Params.Add("cy", distortionParam.Height);

            msg.Params.Add("type", distortionParam.type);
            msg.Params.Add("sType", distortionParam.sType);
            msg.Params.Add("lType", distortionParam.lType);
            msg.Params.Add("dType", distortionParam.dType);


            msg.Params.Add("file_data", ToJsonFileList(ImageChannelType.Gray_Y, FileName));

            return PublishAsyncClient(msg);
        }



        public MsgRecord FocusPoints(string FileName, FocusPointsParam focusPointsParam)
        {
            var Params = new Dictionary<string, object>() { { "nBatchID", -1 } };

            MsgSend msg = new MsgSend
            {
                EventName = "FocusPoints",
                Params = Params
            };
            Params.Add("file_data", ToJsonFileList(ImageChannelType.Gray_Y, FileName));
            return PublishAsyncClient(msg);
        }

        public MsgRecord LedCheck(string FileName, LedCheckParam ledCheckParam)
        {
            var Params = new Dictionary<string, object>() { { "nBatchID", -1 } };

            MsgSend msg = new MsgSend
            {
                EventName = "LedCheck",
                Params = Params
            };
            Params.Add("checkChannel", ledCheckParam.CheckChannel);
            Params.Add("isguding", ledCheckParam.Isguding);
            Params.Add("gudingrid", ledCheckParam.Gudingrid);
            Params.Add("lunkuomianji", ledCheckParam.Lunkuomianji);
            Params.Add("pointNum", ledCheckParam.PointNum);
            Params.Add("hegexishu", ledCheckParam.Hegexishu);
            Params.Add("erzhihuapiancha", ledCheckParam.Erzhihuapiancha);
            Params.Add("binaryCorret", ledCheckParam.BinaryCorret);
            Params.Add("boundry", ledCheckParam.Boundry);
            Params.Add("isuseLocalRdPoint", ledCheckParam.IsuseLocalRdPoint);
            Params.Add("picwid", ledCheckParam.Picwid);
            Params.Add("pichig", ledCheckParam.Pichig);
            Params.Add("LengthCheck", ledCheckParam.LengthCheck ?? Array.Empty<double>());
            Params.Add("LengthRange", ledCheckParam.LengthRange ?? Array.Empty<double>());
            Params.Add("localRdMark", ledCheckParam.LocalRdMark ?? Array.Empty<double>());
            Params.Add("file_data", ToJsonFileList(ImageChannelType.Gray_Y, FileName));
            return PublishAsyncClient(msg,60000);
        }


        public MsgRecord Close()
        {
            MsgSend msg = new MsgSend {  EventName = "Close" };
            return PublishAsyncClient(msg);
        }

        internal void Open(string fileName)
        {
            MsgSend msg = new MsgSend
            {
                EventName = MQTTFileServerEventEnum.Event_File_Download,
                ServiceName = Config.Code,
                Params = new Dictionary<string, object> { { "FileName", fileName }, { "FileExtType", FileExtType.CIE } }
            };
            PublishAsyncClient(msg);
        }

        internal void UploadCIEFile(string fileName)
        {
            MsgSend msg = new MsgSend
            {
                EventName = MQTTFileServerEventEnum.Event_File_Upload,
                ServiceName = Config.Code,
                Params = new Dictionary<string, object> { { "FileName", fileName }, { "FileExtType", FileExtType.CIE } }
            };
            PublishAsyncClient(msg);
        }
    }
}
