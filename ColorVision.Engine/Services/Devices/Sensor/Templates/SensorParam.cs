﻿using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates;
using MQTTMessageLib.Sensor;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Sensor.Templates
{
    public class TemplateSensor : ITemplate<SensorParam>
    {
        public static Dictionary<string, ObservableCollection<TemplateModel<SensorParam>>> Params { get; set; } = new Dictionary<string, ObservableCollection<TemplateModel<SensorParam>>>();

        public TemplateSensor(string code)
        {
            Code = code;
            if (Params.TryGetValue(Code, out var templatesParams))
            {
                TemplateParams = templatesParams;
            }
            else
            {
                templatesParams = new ObservableCollection<TemplateModel<SensorParam>>();
                TemplateParams = templatesParams;
                Params.Add(Code,templatesParams);
            }
            IsUserControl = true;
        }


        public override string Title { get => Code + ColorVision.Engine.Properties.Resources.Edit; set { } }

        public EditTemplateSensor EditTemplateSensor { get; set; } = new EditTemplateSensor();

        public override UserControl GetUserControl() => EditTemplateSensor;

        public override void SetUserControlDataContext(int index)
        {
            EditTemplateSensor.SetParam(TemplateParams[index].Value);
        }
    }

    public class SensorCommand:ViewModelBase
    {
        public ModDetailModel Model { get; set; }
        public SensorCommand(ModDetailModel modDetailModel)
        {
            Model = modDetailModel;
            ParseRequestString();
        }

        public void ParseRequestString()
        {
            string? str = Model.ValueA;
            if (str == null)
            {
                GenerateRequestString();
            }
            else
            {
                var parts = str.Split(',');

                if (parts.Length >= 5)
                {
                    Request = parts[0];
                    Response = parts[1];

                    // 解析 CmdType（SensorCmdType）
                    if (Enum.TryParse(parts[2], out SensorCmdType cmdType))
                    {
                        SensorCmdType = cmdType;
                    }

                    // 解析 Timeout 和 Delay
                    var timeParts = parts[3].Split('/');
                    if (timeParts.Length == 2)
                    {
                        if (int.TryParse(timeParts[0], out int timeout))
                            Timeout = timeout;

                        if (int.TryParse(timeParts[1], out int delay))
                            Delay = delay;
                    }
                    // 解析 RetryCount
                    if (int.TryParse(parts[4], out int retryCount))
                        RetryCount = retryCount;
                }

            }
        }


        public void GenerateRequestString()
        {
            Model.ValueA = $"{Request},{Response},{SensorCmdType},{Timeout}/{Delay},{RetryCount}";
        }

        public SensorCmdType SensorCmdType { get => _SensorCmdType; set { _SensorCmdType = value; NotifyPropertyChanged(); GenerateRequestString(); } }
        private SensorCmdType _SensorCmdType = SensorCmdType.Ascii;

        public string Request { get => _Request; set { _Request = value; NotifyPropertyChanged (); GenerateRequestString(); } }
        private string _Request = string.Empty;
        public string Response { get => _Response; set { _Response = value; NotifyPropertyChanged(); GenerateRequestString(); } }
        private string _Response = string.Empty;

        public int Timeout { get => _Timeout; set { _Timeout = value; NotifyPropertyChanged(); GenerateRequestString(); } }
        private int _Timeout = 1000;

        public int Delay { get => _Delay; set { _Delay = value; NotifyPropertyChanged(); GenerateRequestString(); } }
        private int _Delay;

        public int RetryCount { get => _RetryCount; set { _RetryCount = value; NotifyPropertyChanged(); GenerateRequestString(); } }
        private int _RetryCount;
    }



    public class SensorParam:ParamBase
    {
        public SensorParam() : base()
        {

        }
       public ObservableCollection<SensorCommand> SensorCommands { get; set; }

        public SensorParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {
            SensorCommands = new ObservableCollection<SensorCommand>();
            foreach (var mod in ModDetailModels)
            {
                SensorCommands.Add(new SensorCommand(mod));
            }
            // 监听 ModDetailModels 的更改
            ModDetailModels.CollectionChanged += (s, e) =>
            {
                switch (e.Action)
                {
                    // 当有新项被添加时，向 SensorCommands 中添加相应的 SensorCommand
                    case NotifyCollectionChangedAction.Add:
                        foreach (ModDetailModel newMod in e.NewItems)
                        {
                            SensorCommands.Add(new SensorCommand(newMod));
                        }
                        break;
                    // 当有项被移除时，移除对应的 SensorCommand
                    case NotifyCollectionChangedAction.Remove:
                        foreach (ModDetailModel oldMod in e.OldItems)
                        {
                            var commandToRemove = SensorCommands.FirstOrDefault(c => c.Model == oldMod);
                            if (commandToRemove != null)
                            {
                                SensorCommands.Remove(commandToRemove);
                            }
                        }
                        break;

                    // 当某项被替换时，更新对应的 SensorCommand
                    case NotifyCollectionChangedAction.Replace:
                        for (int i = 0; i < e.NewItems.Count; i++)
                        {
                            var oldMod = (ModDetailModel)e.OldItems[i];
                            var newMod = (ModDetailModel)e.NewItems[i];

                            var commandToReplace = SensorCommands.FirstOrDefault(c => c.Model == oldMod);
                            if (commandToReplace != null)
                            {
                                // 替换为新的 SensorCommand
                                int index = SensorCommands.IndexOf(commandToReplace);
                                SensorCommands[index] = new SensorCommand(newMod);
                            }
                        }
                        break;

                    // 当集合被重置时，清空并重新填充
                    case NotifyCollectionChangedAction.Reset:
                        SensorCommands.Clear();
                        foreach (var mod in ModDetailModels)
                        {
                            SensorCommands.Add(new SensorCommand(mod));
                        }
                        break;
                }
            };
        }

    }
}
  