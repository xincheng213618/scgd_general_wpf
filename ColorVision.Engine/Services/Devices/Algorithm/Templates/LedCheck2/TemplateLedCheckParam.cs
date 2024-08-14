using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates;
using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.LedCheck2
{
    public class TemplateLedCheck2Param : ITemplate<LedCheck2Param>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<LedCheck2Param>> Params { get; set; } = new ObservableCollection<TemplateModel<LedCheck2Param>>();


        public TemplateLedCheck2Param()
        {
            Title = "灯珠检测2算法配置";
            Code = "LedCheck2";
            TemplateParams = Params;
            IsUserControl = true;
        }

        public override void SetUserControlDataContext(int index)
        {
            EditTemplate.SetParam(TemplateParams[index].Value);
        }

        public EditLedCheck2 EditTemplate { get; set; } = new EditLedCheck2();

        public override UserControl GetUserControl()
        {
            return EditTemplate;
        }


    }
}
