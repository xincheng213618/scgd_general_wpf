﻿using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using MQTTMessageLib;
using MQTTMessageLib.Algorithm;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Matching
{
    public class AlgorithmMatching : ViewModelBase, IDisplayAlgorithm
    {
        public string Name { get; set; } = "模板匹配";
        public int Order { get; set; } = 99;

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public RelayCommand OpenTemplatePoiCommand { get; set; }
        public RelayCommand OpenTemplateCommand { get; set; }

        public RelayCommand SetTemplateFileCommand { get; set; }


        public AlgorithmMatching(DeviceAlgorithm deviceAlgorithm)
        {
            Device = deviceAlgorithm;
            OpenTemplateCommand = new RelayCommand(a => OpenTemplate());
            OpenTemplatePoiCommand = new RelayCommand(a => OpenTemplatePoi());
            SetTemplateFileCommand = new RelayCommand(a => SetFile(this, nameof(TemplateFile)));

        }


        public int TemplateSelectedIndex { get => _TemplateSelectedIndex; set { _TemplateSelectedIndex = value; NotifyPropertyChanged(); } }
        private int _TemplateSelectedIndex;

        public void OpenTemplate()
        {
            new TemplateEditorWindow(new TemplateMatch(), TemplateSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }

        public void OpenTemplatePoi()
        {
            new TemplateEditorWindow(new TemplatePoi(), _TemplatePoiSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }
        public int TemplatePoiSelectedIndex { get => _TemplatePoiSelectedIndex; set { _TemplatePoiSelectedIndex = value; NotifyPropertyChanged(); } }
        private int _TemplatePoiSelectedIndex;


        public string TemplateFile { get => _TemplateFile; set { _TemplateFile = value; NotifyPropertyChanged(); } }
        private string _TemplateFile;
        public static void SetFile(object target, string propertyName)
        {
            using (System.Windows.Forms.OpenFileDialog saveFileDialog = new System.Windows.Forms.OpenFileDialog())
            {
                saveFileDialog.Filter = "Tif Files (*.tif)|*.txt|All Files (*.*)|*.*";
                saveFileDialog.Title = "打开文件";
                saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                saveFileDialog.RestoreDirectory = true;
                if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    PropertyInfo prop = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                    if (prop != null && prop.CanWrite)
                    {
                        prop.SetValue(target, saveFileDialog.FileName, null);
                    }
                }
            }
        }


        public UserControl GetUserControl()
        {
            UserControl ??= new DisplayMatching(this);
            return UserControl;
        }
        public UserControl UserControl { get; set; }

        public MsgRecord SendCommand(string deviceCode, string deviceType, string fileName, FileExtType fileExtType ,string serialNumber)
        {
            MatchParam Param = TemplateMatch.Params[TemplateSelectedIndex].Value;
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateFile", TemplateFile);
            Params.Add("TemplateParam", new CVTemplateParam() { ID = Param.Id, Name = Param.Name });

            MsgSend msg = new()
            {
                EventName = MQTTAlgorithmEventEnum.Event_MatchTemplate,
                SerialNumber = sn,
                Params = Params
            };

            return DService.PublishAsyncClient(msg);
        }
    }
}