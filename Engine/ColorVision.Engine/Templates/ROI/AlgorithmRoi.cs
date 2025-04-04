﻿using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using MQTTMessageLib;
using MQTTMessageLib.Algorithm;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.ROI
{
    public class AlgorithmRoi : ViewModelBase, IDisplayAlgorithm
    {
        public string Name { get; set; } = "发光区检测";
        public int Order { get; set; } = 11;


        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public RelayCommand OpenTemplateCommand { get; set; }

        public AlgorithmRoi(DeviceAlgorithm deviceAlgorithm)
        {
            Device = deviceAlgorithm;
            OpenTemplateCommand = new RelayCommand(a => OpenTemplate());
        }
        public int TemplateSelectedIndex { get => _TemplateSelectedIndex; set { _TemplateSelectedIndex = value; NotifyPropertyChanged(); } }
        private int _TemplateSelectedIndex;

        public void OpenTemplate()
        {
            new TemplateEditorWindow(new TemplateRoi(), TemplateSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }

        public UserControl GetUserControl()
        {
            UserControl ??= new DisplayRoi(this);
            return UserControl;
        }
        public UserControl UserControl { get; set; }


        public MsgRecord SendCommand(RoiParam param, string deviceCode, string deviceType, string fileName, FileExtType fileExtType, string serialNumber)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = param.Id, Name = param.Name });

            MsgSend msg = new()
            {
                EventName = MQTTAlgorithmEventEnum.Event_LightArea2_GetData,
                SerialNumber = sn,
                Params = Params
            };

            return DService.PublishAsyncClient(msg);
        }
    }
}
