using LocalizedText = global::ColorVision.Engine.EngineLocalization;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.Ghost2
{

    public class TemplateJsonGhost2 : TemplateJsonParam
    {
        private static ILog log = LogManager.GetLogger(nameof(TemplateJsonGhost2));

        public GhostDetectionConfig KBJson
        {
            get
            {
                try
                {
                    GhostDetectionConfig kBJson = JsonConvert.DeserializeObject<GhostDetectionConfig>(JsonValue);
                    if (kBJson == null)
                    {
                        kBJson = new GhostDetectionConfig();
                        JsonValue = JsonConvert.SerializeObject(kBJson);
                        return kBJson;
                    }
                    return kBJson;
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                    GhostDetectionConfig kBJson = new GhostDetectionConfig();
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



        public TemplateJsonGhost2() : base()
        {
        }

        public TemplateJsonGhost2(ModMasterModel templateJsonModel) : base(templateJsonModel)
        {

        }


    }

    public class TemplateGhostQK : ITemplateJson<TemplateJsonGhost2>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TemplateJsonGhost2>> Params { get; set; } = new ObservableCollection<TemplateModel<TemplateJsonGhost2>>();

        public TemplateGhostQK()
        {
            Title = LocalizedText.Get("Ghost2.0模板管理");
            Code = "ghost";
            Name = "ghost2.0";
            TemplateDicId = 38;
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
