using ColorVision.Database;
using ColorVision.Engine.Rbac;
using ColorVision.Engine.Templates;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ColorVision.Engine.Services.Devices.Sensor.Templates.Dic
{
    public class TemplateSensorDicModParam : TemplateModParam
    {
        public new static ObservableCollection<TemplateModel<DicModParam>> Params { get; set; } = new ObservableCollection<TemplateModel<DicModParam>>();

        public TemplateSensorDicModParam()
        {
            Title = "传感器字典编辑";
            TemplateParams = Params;
            IsUserControl = true;
            Code = "Sensor";
        }

        public override void Create(string templateCode, string templateName)
        {
            SysDictionaryModModel sysDictionaryModModel = new SysDictionaryModModel() { Name = templateName, Code = templateCode, ModType = 5 };
            SysDictionaryModMasterDao.Instance.Save(sysDictionaryModModel);
            var list = SysDictionaryModDetailDao.Instance.GetAllByPid(sysDictionaryModModel.Id);
            var t = new DicModParam(sysDictionaryModModel, list);
            var templateModel = new TemplateModel<DicModParam>(t.Name ?? "default", t);
            TemplateParams.Add(templateModel);
        }

        public override void Load()
        {
            var backup = TemplateParams.ToDictionary(tp => tp.Id, tp => tp);

            if (MySqlSetting.Instance.IsUseMySql && MySqlSetting.IsConnect)
            {
                var models = SysDictionaryModMasterDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "tenant_id", 0}, { "mod_type", 5 } });
                foreach (var model in models)
                {
                    var list = SysDictionaryModDetailDao.Instance.GetAllByPid(model.Id);
                    var t = new DicModParam(model, list);
                    if (backup.TryGetValue(t.Id, out var template))
                    {
                        template.Value = t;
                        template.Key = t.Name;
                    }
                    else
                    {
                        var templateModel = new TemplateModel<DicModParam>(t.Name ?? "default", t);
                        TemplateParams.Add(templateModel);
                    }
                }
            }
        }


    }

}
