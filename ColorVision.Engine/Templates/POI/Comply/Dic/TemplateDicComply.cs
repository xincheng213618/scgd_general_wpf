﻿using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Rbac;
using ColorVision.Engine.Services.SysDictionary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.POI.Comply.Dic
{
    public class TemplateDicComply : ITemplate<DicComplyParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<DicComplyParam>> Params { get; set; } = new ObservableCollection<TemplateModel<DicComplyParam>>();

        public TemplateDicComply()
        {
            Title = "编辑默认合规模板";
            TemplateParams = Params;
            IsUserControl = true;
        }

        private DicEditComply DicEditComply { get; set; } = new DicEditComply();

        public override UserControl GetUserControl() => DicEditComply;

        public override void SetUserControlDataContext(int index)
        {
            DicEditComply.SetParam(TemplateParams[index].Value);
        }

        public override void Load()
        {
            var backup = TemplateParams.ToDictionary(tp => tp.Id, tp => tp);
            if (MySqlSetting.Instance.IsUseMySql && MySqlSetting.IsConnect)
            {
                void Add(int mod_type)
                {
                    var models = SysDictionaryModDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "tenant_id", UserConfig.Instance.TenantId }, { "mod_type", mod_type } });
                    foreach (var model in models)
                    {
                        var list = SysDictionaryModItemValidateDao.Instance.GetAllByPid(model.Id);
                        var t = new DicComplyParam(model, list);
                        if (backup.TryGetValue(t.Id, out var template))
                        {
                            template.Value = t;
                            template.Key = t.Name;
                        }
                        else
                        {
                            var templateModel = new TemplateModel<DicComplyParam>(t.Name ?? "default", t);
                            TemplateParams.Add(templateModel);
                        }
                    }
                }
                Add(110);
                Add(111);
            }
        }


        public override void Save()
        {
            if (SaveIndex.Count == 0) return;

            foreach (var index in SaveIndex)
            {
                if (index > -1 && index < TemplateParams.Count)
                {
                    var item = TemplateParams[index];
                    var modMasterModel = SysDictionaryModDao.Instance.GetById(item.Value.Id);
                    SysDictionaryModDao.Instance.Save(item.Value.ModMasterModel);
                    foreach (var modDetaiModel in TemplateParams[index].Value.ModDetaiModels)
                    {
                        SysDictionaryModItemValidateDao.Instance.Save(modDetaiModel);
                    }
                }
            }
        }

        public override void OpenCreate()
        {
            CreateDicTemplate createDicTemplate = new CreateDicTemplate(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            createDicTemplate.ShowDialog();

        }

        public override void Create(string templateCode, string templateName)
        {
            SysDictionaryModModel sysDictionaryModModel = new SysDictionaryModModel() { Name = templateName, Code = templateCode, ModType = 7 };
            SysDictionaryModDao.Instance.Save(sysDictionaryModModel);
            var t = new DicComplyParam(sysDictionaryModModel, new List<SysDictionaryModItemValidateModel>());

            var templateModel = new TemplateModel<DicComplyParam>(t.Name ?? "default", t);
            TemplateParams.Add(templateModel);
        }
    }


}
