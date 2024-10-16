﻿
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao;
using ColorVision.Engine.Templates;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using ColorVision.UI.Utilities;
using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql.ORM;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates
{

    public class ModThirdPartyParam : ParamBase
    {

        public RelayCommand ResetCommand { get; set; }


        public ModThirdPartyParam() 
        {
            ResetCommand = new RelayCommand((a)=> ResetValue());
        }

        public ModThirdPartyParam(ModThirdPartyAlgorithmsModel modThirdPartyAlgorithmsModel)
        {
            ModThirdPartyAlgorithmsModel = modThirdPartyAlgorithmsModel;
            ResetCommand = new RelayCommand((a) => ResetValue());
        }

        public void ResetValue()
        {
            if (ModThirdPartyAlgorithmsModel.PId is int pid && ThirdPartyAlgorithmsDao.Instance.GetById(pid)?.DefaultCfg is string str)
            {
                JsonValue = str;
            }
            else
            {
                MessageBox.Show("无法重置，请检查数据库相关配置");
            }
        }

        public override int Id { get => ModThirdPartyAlgorithmsModel.Id; set { ModThirdPartyAlgorithmsModel.Id = value; NotifyPropertyChanged(); } }
        public override string Name { get => ModThirdPartyAlgorithmsModel.Name ?? string.Empty; set { ModThirdPartyAlgorithmsModel.Name = value; NotifyPropertyChanged(); } }
        public ModThirdPartyAlgorithmsModel ModThirdPartyAlgorithmsModel { get; set; }

        public string JsonValue
        {
            get => JsonHelper.BeautifyJson(ModThirdPartyAlgorithmsModel.JsonVal); set
            {
                if (JsonHelper.IsValidJson(value))
                {
                    ModThirdPartyAlgorithmsModel.JsonVal = value;
                    NotifyPropertyChanged();
                }
            }
        }

    }
}
