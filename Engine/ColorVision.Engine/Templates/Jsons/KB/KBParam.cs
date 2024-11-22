using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates;
using NPOI.SS.Formula.Functions;
using NPOI.XWPF.UserModel;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.KB
{
    public class KBParam : TemplateJsonParam
    {
        public KBParam():base (new TemplateJsonModel())
        {
        }
        public KBParam(TemplateJsonModel templateJsonModel) : base(templateJsonModel)
        {

        }
    }

    public class ExportKBTemplate : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplateAlgorithm";
        public override string GuidId => nameof(ExportKBTemplate);
        public override string Header => "ExportKBTemplate";
        public override int Order => 2;
        public override ITemplate Template => new TemplateKB();
    }

    public class TemplateKB : ITemplateJson<KBParam>
    {
        public static ObservableCollection<TemplateModel<KBParam>> Params { get; set; } = new ObservableCollection<TemplateModel<KBParam>>();

        public TemplateKB()
        {
            Title = "KB算法设置";
            Code = "KB";
            TemplateParams = Params;
            IsUserControl = true;
        }

        public override void SetUserControlDataContext(int index)
        {
            EditTemplateJson.SetParam(TemplateParams[index].Value);
        }
        public EditTemplateJson EditTemplateJson { get; set; } = new EditTemplateJson();

        public override UserControl GetUserControl()
        {
            return EditTemplateJson;
        }

    }




}
