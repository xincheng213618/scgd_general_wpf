
using ColorVision.Engine.Templates;
using ColorVision.UI.Utilities;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.LedCheck2
{
    public enum CVOLEDCOLOR
    {
        BLUE = 0,
        GREEN = 1,
        RED = 2,
    };

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
