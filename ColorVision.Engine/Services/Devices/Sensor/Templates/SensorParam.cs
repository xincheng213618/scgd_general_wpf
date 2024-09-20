using ColorVision.Engine.Templates;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        public EditTemplateModeDetail EditTemplateModeDetail { get; set; } = new EditTemplateModeDetail();

        public override UserControl GetUserControl() => EditTemplateModeDetail;

        public override void SetUserControlDataContext(int index)
        {
            EditTemplateModeDetail.SetParam(TemplateParams[index].Value);
        }
    }

    public class SensorParam:ParamBase
    {
        public SensorParam() : base()
        {

        }
        public SensorParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {

        }
    }
}
  