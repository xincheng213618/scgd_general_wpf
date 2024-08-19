
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao;
using ColorVision.Engine.Templates;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using ColorVision.UI.Utilities;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates
{

    public class ModThirdPartyParam : ParamBase
    {

        public ModThirdPartyParam() 
        {
        }

        public ModThirdPartyParam(ModThirdPartyAlgorithmsModel modThirdPartyAlgorithmsModel)
        {
            ModThirdPartyAlgorithmsModel = modThirdPartyAlgorithmsModel;
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
