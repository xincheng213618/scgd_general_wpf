#pragma warning disable CS8603
using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Flow;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Linq;

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


        public ObservableCollection<TemplateModel<FlowParam>> GetFlows()
        {
            var templateModels = new ObservableCollection<TemplateModel<FlowParam>>();
            foreach (var flow in LargeFlowConfig.Flows)
            {
                var templateModel = TemplateFlow.Params.FirstOrDefault(x => x.Value.Name == flow);
                if (templateModel != null)
                {
                    templateModels.Add(templateModel);
                }
            }

            return templateModels;
        }

        public TJLargeFlowParam() : base()
        {
        }

        public TJLargeFlowParam(TemplateJsonModel templateJsonModel):base(templateJsonModel) 
        {

        }


    }




}
