using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.FOV2
{

    public class TJDFOVParam : TemplateJsonParam
    {
        private static ILog log = LogManager.GetLogger(nameof(TJDFOVParam));

        public FovJson Json
        {
            get
            {
                try
                {
                    FovJson kBJson = JsonConvert.DeserializeObject<FovJson>(JsonValue);
                    if (kBJson == null)
                    {
                        kBJson = new FovJson();
                        JsonValue = JsonConvert.SerializeObject(kBJson);
                        return kBJson;
                    }
                    return kBJson;
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                    FovJson kBJson = new FovJson();
                    JsonValue = JsonConvert.SerializeObject(kBJson);
                    return kBJson;
                }
            }
            set
            {
                JsonValue = JsonConvert.SerializeObject(value);
                OnPropertyChanged();
            }
        }



        public TJDFOVParam() : base()
        {
        }

        public TJDFOVParam(ModMasterModel templateJsonModel) : base(templateJsonModel)
        {

        }


    }

    public class TemplateDFOV : ITemplateJson<TJDFOVParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TJDFOVParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TJDFOVParam>>();

        public TemplateDFOV()
        {
            Title = ColorVision.Engine.Properties.Resources.Fov20TemplateManagement;
            Code = "FOV";
            Name = "FOV2.0";
            TemplateDicId = 39;
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
