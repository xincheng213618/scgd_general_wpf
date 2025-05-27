using ColorVision.Engine.MySql;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.MTF2
{

    public class TJMTFParam : TemplateJsonParam
    {
        private static ILog log = LogManager.GetLogger(nameof(TJMTFParam));

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
                NotifyPropertyChanged();
            }
        }



        public TJMTFParam() : base()
        {
        }

        public TJMTFParam(TemplateJsonModel templateJsonModel) : base(templateJsonModel)
        {

        }


    }

    public class TemplateMTF2 : ITemplateJson<TJMTFParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TJMTFParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TJMTFParam>>();

        public TemplateMTF2()
        {
            Title = "MTFV2模板管理";
            Code = "MTF";
            Name = "MTF_V2";
            TemplateDicId = 48;
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
            EditTemplateJson = EditTemplateJson ?? new EditTemplateJson(Description);
            return EditTemplateJson;
        }
        public string Description { get; set; } = "";

        public override UserControl CreateUserControl() => new EditTemplateJson(Description);
        public override IMysqlCommand? GetMysqlCommand() => new MysqlMTF2();

    }




}
