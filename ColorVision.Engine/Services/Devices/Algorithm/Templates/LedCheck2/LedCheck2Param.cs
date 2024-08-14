using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao;
using ColorVision.Engine.Templates;
using ColorVision.UI.Utilities;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.LedCheck2
{

    public class LedCheck2Param : ParamBase
    {

        public LedCheck2Param() { }
        public LedCheck2Param(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {

        }

        public string JsonValue
        {
            get => JsonHelper.BeautifyJson(GetValue(_JsonValue)); set
            {
                if (JsonHelper.IsValidJson(value))
                {
                    SetProperty(ref _JsonValue, value);
                }
            }
        }
        private string _JsonValue;
    }
}
