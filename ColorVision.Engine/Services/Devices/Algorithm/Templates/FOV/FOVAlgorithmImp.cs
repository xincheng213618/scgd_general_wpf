﻿using ColorVision.Engine.Services.Msg;
using MQTTMessageLib;
using MQTTMessageLib.Algorithm;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates;
using System.Windows;
using ColorVision.Common.Utilities;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.FOV
{
    public class FOVAlgorithmImp : ViewModelBase, IAlgorithm
    {
        public string Name { get; set; } = "FOV";

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public RelayCommand OpenTeplateCommand { get; set; }

        public FOVAlgorithmImp(DeviceAlgorithm deviceAlgorithm)
        {
            Device = deviceAlgorithm;
            OpenTeplateCommand = new RelayCommand(a => OpenTeplate());
        }

        public void OpenTeplate()
        {
            new WindowTemplate(new TemplateFOVParam(), TemplateSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public int TemplateSelectedIndex { get => _TemplateSelectedIndex; set { _TemplateSelectedIndex = value; NotifyPropertyChanged(); } }
        private int _TemplateSelectedIndex;


        public UserControl GetUserControl() => UserControl ?? new FOVUserControl(this);
        public UserControl UserControl { get; set; }


        public MsgRecord FOV(string deviceCode, string deviceType, string fileName, FileExtType fileExtType, int pid, string tempName, string serialNumber)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = pid, Name = tempName });

            MsgSend msg = new()
            {
                EventName = MQTTAlgorithmEventEnum.Event_FOV_GetData,
                SerialNumber = sn,
                Params = Params
            };

            return DService.PublishAsyncClient(msg);
        }
    }
}