using log4net;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.LEDStripDetectionV2
{

    public class TJLEDStripDetectionV2Param : TemplateJsonParam
    {
        private static ILog log = LogManager.GetLogger(nameof(TJLEDStripDetectionV2Param));

        public TJLEDStripDetectionV2Param() : base()
        {
        }

        public TJLEDStripDetectionV2Param(ModMasterModel templateJsonModel) : base(templateJsonModel)
        {

        }


    }

    public class TemplateLEDStripDetectionV2 : ITemplateJson<TJLEDStripDetectionV2Param>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TJLEDStripDetectionV2Param>> Params { get; set; } = new ObservableCollection<TemplateModel<TJLEDStripDetectionV2Param>>();

        public TemplateLEDStripDetectionV2()
        {
            Title = ColorVision.Engine.Properties.Resources.LedStripDetectionV2TemplateManagement;
            Code = "LEDStripDetection";
            Name = "LedStripDetectionV2";
            TemplateDicId = 26;
            TemplateParams = Params;
            IsUserControl = true;
        }


        public override void SetUserControlDataContext(int index)
        {
            EditTemplateJson.SetParam(TemplateParams[index].Value);
        }
        public EditTemplateJson EditTemplateJson { get; set; }

        public override UserControl GetUserControl()
        {
            EditTemplateJson = new EditTemplateJson();
            return EditTemplateJson;
        }
        public override UserControl CreateUserControl() => new EditTemplateJson();
    }




}
