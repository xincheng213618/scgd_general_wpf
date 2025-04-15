#pragma warning disable CS8602
using ColorVision.Engine.Messages;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Configs;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Net;
using CVCommCore;
using MQTTMessageLib.FileServer;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Algorithm
{

    public class MQTTAlgorithm : MQTTDeviceService<ConfigAlgorithm>
    {
        public DeviceAlgorithm Device { get; set; }
        private NetFileUtil netFileUtil = new NetFileUtil();


        public MQTTAlgorithm(DeviceAlgorithm device, ConfigAlgorithm Config) : base(Config)
        {
            Device = device;
            MsgReturnReceived += MQTTAlgorithm_MsgReturnReceived;   
            DeviceStatus = DeviceStatusType.Unknown;
            netFileUtil.handler += NetFileUtil_handler;
        }
        private void NetFileUtil_handler(object sender, NetFileEvent arg)
        {
            if (arg.Code == 0 && arg.FileData.data != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Device.View.OpenImage(arg.FileData);
                });
            }
        }

        private void MQTTAlgorithm_MsgReturnReceived(MsgReturn msg)
        {
            if (msg.DeviceCode != Config.Code) return;
            switch (msg.EventName)
            {
                case MQTTFileServerEventEnum.Event_File_List_All:
                    DeviceListAllFilesParam data = JsonConvert.DeserializeObject<DeviceListAllFilesParam>(JsonConvert.SerializeObject(msg.Data));
                    switch (data.FileExtType)
                    {
                        case FileExtType.Raw:
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                data.Files.Reverse();
                                RawImageFiles = data.Files;
                                foreach (var item in RawImageFiles)
                                {
                                    if (!ImageFiles.Contains(item))
                                        ImageFiles.Add(item);
                                }
                            });
                            break;
                        case FileExtType.Src:
                            break;
                        case FileExtType.CIE:
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                data.Files.Reverse();
                                CIEImageFiles = data.Files;
                                foreach (var item in CIEImageFiles)
                                {
                                    if (!ImageFiles.Contains(item))
                                        ImageFiles.Add(item);
                                }
                            });
                            break;
                        case FileExtType.Calibration:
                            break;
                        case FileExtType.Tif:
                            break;
                        default:
                            break;
                    }
                    break;
                case MQTTFileServerEventEnum.Event_File_Download:
                    DeviceFileUpdownParam pm_dl = JsonConvert.DeserializeObject<DeviceFileUpdownParam>(JsonConvert.SerializeObject(msg.Data));
                    if (pm_dl != null)
                    {
                        if (!string.IsNullOrWhiteSpace(pm_dl.FileName)) netFileUtil.TaskStartDownloadFile(pm_dl.IsLocal, pm_dl.ServerEndpoint, pm_dl.FileName, (CVType)FileExtType.CIE);
                    }
                    break;
                default:
                    switch (msg.Code)
                    {
                        case -1:
                            AlgResultMasterModel algResultMasterModel = new AlgResultMasterModel();
                            algResultMasterModel.Result = "-1";

                            Application.Current.Dispatcher.BeginInvoke(() =>
                            {
                                Device.View.AlgResultMasterModelDataDraw(algResultMasterModel);
                            });
                            break;

                        default:
                            List<AlgResultMasterModel> resultMaster = null;
                            if (msg.Data.MasterId > 0)
                            {
                                resultMaster = new List<AlgResultMasterModel>();
                                int MasterId = msg.Data.MasterId;
                                AlgResultMasterModel model = AlgResultMasterDao.Instance.GetById(MasterId);
                                if (model != null)
                                    resultMaster.Add(model);
                            }
                            foreach (AlgResultMasterModel result in resultMaster)
                            {
                                Application.Current.Dispatcher.BeginInvoke(() =>
                                {
                                    Device.View.AlgResultMasterModelDataDraw(result);
                                });
                            }
                            break;
                    }

                    break;
            }
        }

        public ObservableCollection<string> ImageFiles { get => _ImageFiles; set { _ImageFiles = value; NotifyPropertyChanged(); } }
        private ObservableCollection<string> _ImageFiles = new ObservableCollection<string>();

        public List<string> RawImageFiles { get => _RawImageFiles; set { _RawImageFiles = value; NotifyPropertyChanged(); } } 
        private List<string> _RawImageFiles = new List<string>();
        public List<string> CIEImageFiles { get => _CIEImageFiles; set { _CIEImageFiles = value; NotifyPropertyChanged(); } }
        private List<string> _CIEImageFiles = new List<string>();

        public Dictionary<string, string> HistoryFilePath { get; set; } = new Dictionary<string, string>() { };

        public void GetRawFiles(string deviceCode, string deviceType)
        {

            if (!GetHistoryPath(deviceCode))
            {
                MsgSend msg = new()
                {
                    EventName = MQTTFileServerEventEnum.Event_File_List_All,
                    Params = new Dictionary<string, object> { { "FileExtType", FileExtType.Raw }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } }
                };
                PublishAsyncClient(msg);
            }

        }
        public bool GetHistoryPath(string deviceCode)
        {
            var deviceServices = ServiceManager.GetInstance().DeviceServices;
            var targetService = deviceServices.FirstOrDefault(service =>
                service.GetConfig() is IFileServerCfg fileServerCfg && service.Code == deviceCode);

            string path = string.Empty;
            if (targetService != null && targetService.GetConfig() is IFileServerCfg fileServerCfg)
            {
                path = fileServerCfg.FileServerCfg.DataBasePath;
            }
            if (Directory.Exists(Config.FileServerCfg.DataBasePath))
            {
                var rawfiles = new List<string>();
                var ciefiles = new List<string>();

                foreach (var item in GetDirectoriesCreatedInLastDays(Path.Combine(Config.FileServerCfg.DataBasePath, deviceCode, "data"), ViewAlgorithmConfig.Instance.HistoyDay))
                {
                    foreach (var item1 in item.GetFiles())
                    {
                        HistoryFilePath.TryAdd(item1.Name, item1.FullName);
                        if (item1.Extension.Contains("cvraw"))
                        {
                            rawfiles.Add(item1.Name);
                        }
                        if (item1.Extension.Contains("cvcie"))
                        {
                            ciefiles.Add(item1.Name);
                        }
                    }
                }
                rawfiles.Reverse();
                ciefiles.Reverse();
                RawImageFiles = rawfiles;
                CIEImageFiles = ciefiles;
                return true;
            }

            return false;
        }

        static List<DirectoryInfo> GetDirectoriesCreatedInLastDays(string directoryPath, int daysRange)
        {
            if (daysRange < 1) daysRange = 1;
            if (daysRange > 365) daysRange = 365;

            List<DirectoryInfo> directories = new List<DirectoryInfo>();
            DateTime currentDate = DateTime.Now;
            DateTime dateLimit = currentDate.AddDays(-daysRange);

            DirectoryInfo dirInfo = new DirectoryInfo(directoryPath);
            if (dirInfo.Exists)
            {
                // 获取所有文件夹
                FileSystemInfo[] fsInfos = dirInfo.GetFileSystemInfos();
                foreach (FileSystemInfo fsInfo in fsInfos)
                {
                    if (fsInfo is DirectoryInfo subDir)
                    {
                        // 检查文件夹是否在指定日期范围内创建
                        if (subDir.CreationTime > dateLimit)
                        {
                            directories.Add(subDir);
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("The specified directory does not exist.");
            }

            return directories;
        }



        public void GetCIEFiles(string deviceCode, string deviceType)
        {
            if (!GetHistoryPath(deviceCode))
            {
                MsgSend msg = new()
                {
                    EventName = MQTTFileServerEventEnum.Event_File_List_All,
                    Params = new Dictionary<string, object> { { "FileExtType", FileExtType.CIE }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } }
                };
                PublishAsyncClient(msg);

            }
        }



        internal void Open(string deviceCode, string deviceType, string fileName, FileExtType extType)
        {

            if (HistoryFilePath.TryGetValue(fileName,out string fullpath) && File.Exists(fullpath))
            {
                Device.View.ImageView.OpenImage(fullpath);
            }
            else
            {
                MsgSend msg = new()
                {
                    EventName = MQTTFileServerEventEnum.Event_File_Download,
                    ServiceName = Config.Code,
                    Params = new Dictionary<string, object> { { "FileName", fileName }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType }, { "FileExtType", extType } }
                };
                PublishAsyncClient(msg);
            }


        }

        public void UploadCIEFile(string fileName)
        {
            MsgSend msg = new()
            {
                EventName = MQTTFileServerEventEnum.Event_File_Upload,
                ServiceName = Config.Code,
                Params = new Dictionary<string, object> { { "FileName", fileName }, { "FileExtType", FileExtType.CIE } }
            };
            PublishAsyncClient(msg);
        }

        public MsgRecord CacheClear()
        {
            return PublishAsyncClient(new MsgSend { EventName = "" });
        }


    }
}
