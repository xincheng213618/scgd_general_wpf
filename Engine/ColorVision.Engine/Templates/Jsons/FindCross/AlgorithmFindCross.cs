﻿using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Templates.POI;
using MQTTMessageLib;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;


namespace ColorVision.Engine.Templates.Jsons.FindCross
{
    [DisplayAlgorithm(53, "十字计算", "Json")]
    public class AlgorithmFindCross : DisplayAlgorithmBase
    {

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public RelayCommand OpenTemplateCommand { get; set; }

        public AlgorithmFindCross(DeviceAlgorithm deviceAlgorithm)
        {
            Device = deviceAlgorithm;
            OpenTemplateCommand = new RelayCommand(a => OpenTemplate());
            OpenTemplatePoiCommand = new RelayCommand(a => OpenTemplatePoi());
        }
        public int TemplateSelectedIndex { get => _TemplateSelectedIndex; set { _TemplateSelectedIndex = value; OnPropertyChanged(); } }
        private int _TemplateSelectedIndex;

        public void OpenTemplate()
        {
            new TemplateEditorWindow(new TemplateFindCross(), TemplateSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }

        public RelayCommand OpenTemplatePoiCommand { get; set; }
        public int TemplatePoiSelectedIndex { get => _TemplatePoiSelectedIndex; set { _TemplatePoiSelectedIndex = value; OnPropertyChanged(); } }
        private int _TemplatePoiSelectedIndex;

        public void OpenTemplatePoi()
        {
            new TemplateEditorWindow(new TemplatePoi(), _TemplatePoiSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }

        public override UserControl GetUserControl()
        {
            UserControl ??= new DisplayFindCross(this);
            return UserControl;
        }
        public UserControl UserControl { get; set; }


        public MsgRecord SendCommand(ParamBase param, string deviceCode, string deviceType, string fileName, FileExtType fileExtType, string serialNumber)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;
            if (DService.HistoryFilePath.TryGetValue(fileName, out string fullpath))
                fileName = fullpath;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = param.Id, Name = param.Name });
            if (TemplatePoiSelectedIndex > -1)
            {
                var poi_pm = TemplatePoi.Params[TemplatePoiSelectedIndex].Value;
                Params.Add("POITemplateParam", new CVTemplateParam() { ID = poi_pm.Id, Name = poi_pm.Name });
            }
            MsgSend msg = new()
            {
                EventName = "FindCross",
                SerialNumber = sn,
                Params = Params
            };
            return DService.PublishAsyncClient(msg);
        }
    }
}
