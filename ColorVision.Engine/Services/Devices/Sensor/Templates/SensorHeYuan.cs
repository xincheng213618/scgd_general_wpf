using ColorVision.Engine.Templates;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Sensor.Templates
{
    public class TemplateSensorHeYuan : ITemplate<SensorHeYuan>
    {
        public static Dictionary<string, ObservableCollection<TemplateModel<SensorHeYuan>>> Params { get; set; } = new Dictionary<string, ObservableCollection<TemplateModel<SensorHeYuan>>>();

        public TemplateSensorHeYuan(string code)
        {
            Code = code;
            if (Params.TryGetValue(Code, out var templatesParams))
            {
                TemplateParams = templatesParams;
            }
            else
            {
                templatesParams = new ObservableCollection<TemplateModel<SensorHeYuan>>();
                TemplateParams = templatesParams;
                Params.Add(Code,templatesParams);
            }
            IsUserControl = true;
        }
        public override string Title { get => Code + "编辑"; set { } }

        public EditTemplateModeDetail EditTemplateModeDetail { get; set; } = new EditTemplateModeDetail();

        public override UserControl GetUserControl() => EditTemplateModeDetail;

        public override void SetUserControlDataContext(int index)
        {
            EditTemplateModeDetail.SetParam(TemplateParams[index].Value);
        }
    }

    public class SensorHeYuan:ParamBase
    {
        public SensorHeYuan() : base()
        {

        }
        public SensorHeYuan(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {

        }
    }
}
