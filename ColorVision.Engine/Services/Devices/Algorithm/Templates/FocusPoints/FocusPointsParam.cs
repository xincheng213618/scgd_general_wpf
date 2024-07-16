using ColorVision.Engine.Templates;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.FocusPoints
{
    public class FocusPointsParam : ParamBase
    {
        public static ObservableCollection<TemplateModel<FocusPointsParam>> FocusPointsParams { get; set; } = new ObservableCollection<TemplateModel<FocusPointsParam>>();

        public FocusPointsParam() { }

        public FocusPointsParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {
        }

        public bool value1 { get; set; }


    }
}
