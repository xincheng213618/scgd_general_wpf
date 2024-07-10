#pragma warning disable CA1707,IDE1006

using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Services.Dao;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using ColorVision.Engine.Templates.POI;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates
{
    public class ExportFocusPoints : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplateAlgorithm";
        public override string GuidId => "FocusPoints";
        public override int Order => 2;
        public override string Header => Properties.Resources.MenuFocusPoints;
        public override ITemplate Template => new TemplateFocusPointsParam();
    }

    public class TemplateFocusPointsParam : ITemplate<FocusPointsParam>, IITemplateLoad
    {
        public TemplateFocusPointsParam()
        {
            Title = "FocusPoints算法设置";
            Code = ModMasterType.FocusPoints;
            TemplateParams = FocusPointsParam.FocusPointsParams;
        }
    }

    public class FocusPointsParam : ParamBase
    {
        public static ObservableCollection<TemplateModel<FocusPointsParam>> FocusPointsParams { get; set; } = new ObservableCollection<TemplateModel<FocusPointsParam>>();

        public FocusPointsParam() { }

        public FocusPointsParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster.Id, modMaster.Name ?? string.Empty, modDetails)
        {
        }

        public bool value1 { get; set; }


    }
}
