using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.Msg;
using ColorVision.Engine.Templates;
using CVCommCore.CVAlgorithm;
using MQTTMessageLib;
using MQTTMessageLib.Algorithm;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POI.BuildPoi
{

    public class AlgorithmBuildPoi : ViewModelBase, IDisplayAlgorithm
    {
        public string Name { get; set; } = "关注点布点";

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public RelayCommand OpenTemplateCommand { get; set; }

        public AlgorithmBuildPoi(DeviceAlgorithm deviceAlgorithm)
        {
            Device = deviceAlgorithm;
            OpenTemplateCommand = new RelayCommand(a => OpenTemplate());
        }
        public int TemplateSelectedIndex { get => _TemplateSelectedIndex; set { _TemplateSelectedIndex = value; NotifyPropertyChanged(); } }
        private int _TemplateSelectedIndex;

        public void OpenTemplate()
        {
            new WindowTemplate(new TemplateBuildPoi(), TemplateSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public UserControl GetUserControl()
        {
            UserControl ??= new DisplayBuildPoi(this);
            return UserControl;
        }
        public UserControl UserControl { get; set; }


        public POIStorageModel POIStorageModel { get => _POIStorageModel; set { _POIStorageModel = value; NotifyPropertyChanged(); } }
        private POIStorageModel _POIStorageModel = POIStorageModel.Db;

        public MsgRecord SendCommand(ParamBuildPoi buildPOIParam, POIPointTypes POILayoutReq, Dictionary<string, object> @params, string deviceCode, string deviceType, string fileName, FileExtType fileExtType, string serialNumber)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = buildPOIParam.Id, Name = buildPOIParam.Name });
            Params.Add("POILayoutReq", POILayoutReq.ToString());
            foreach (var param in @params)
            {
                Params.Add(param.Key, param.Value);
            }
            Params.Add("POIStorageModel", POIStorageModel);
            MsgSend msg = new()
            {
                EventName = MQTTAlgorithmEventEnum.Event_Build_POI,
                SerialNumber = sn,
                Params = Params
            };

            return DService.PublishAsyncClient(msg);
        }
    }
}
