#pragma warning disable CS8603
using ColorVision.Common.MVVM;
using log4net;
using Newtonsoft.Json;
using System;

namespace ColorVision.Engine.Templates.Jsons.LargeFlow
{
    public class TJLargeFlowParam : TemplateJsonParam
    {
        private static ILog log = LogManager.GetLogger(nameof(TJLargeFlowParam));
        public RelayCommand EditTemplatePoiCommand { get; set; }
        public RelayCommand EditCommand { get; set; }

        public LargeFlowConfig  LargeFlowConfig { get 
            {
                try
                {
                    LargeFlowConfig LargeFlowConfig = JsonConvert.DeserializeObject<LargeFlowConfig>(JsonValue);
                    if (LargeFlowConfig == null)
                    {
                        LargeFlowConfig = new LargeFlowConfig();
                        JsonValue = JsonConvert.SerializeObject(LargeFlowConfig);
                        return LargeFlowConfig;
                    }
                    return LargeFlowConfig;  
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                    LargeFlowConfig kBJson = new LargeFlowConfig();
                    JsonValue = JsonConvert.SerializeObject(kBJson);
                    return kBJson;
                }
            }
            set {
                JsonValue = JsonConvert.SerializeObject(value);
                NotifyPropertyChanged(); 
            } }

        public TJLargeFlowParam() : base()
        {
        }

        public TJLargeFlowParam(TemplateJsonModel templateJsonModel):base(templateJsonModel) 
        {

        }


    }




}
