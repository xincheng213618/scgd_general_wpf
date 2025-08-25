#pragma warning disable CS8601
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Jsons;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates
{
    public class TemplateThirdParty : ITemplate<TemplateJsonParam> 
    {
        public static Dictionary<string, ObservableCollection<TemplateModel<TemplateJsonParam>>> Params { get; set; } = new Dictionary<string, ObservableCollection<TemplateModel<TemplateJsonParam>>>();

        public ThirdPartyAlgorithmsModel ThirdPartyAlgorithmsModel { get; set; }

        public TemplateThirdParty(string code)
        {
            Code = code;
            if (Params.TryGetValue(Code, out var templatesParams))
            {
                TemplateParams = templatesParams;
            }
            else
            {
                templatesParams = new ObservableCollection<TemplateModel<TemplateJsonParam>>();
                TemplateParams = templatesParams;
                Params.Add(Code, templatesParams);
            }
            ThirdPartyAlgorithmsModel = ThirdPartyAlgorithmsDao.Instance.GetByParam(new Dictionary<string, object>() { { "code", Code } });
            IsUserControl = true;
        }

        public override string Title { get => Code + ColorVision.Engine.Properties.Resources.Edit; set { } }

        public override void SetUserControlDataContext(int index)
        {
            EditTemplateThird.SetParam(TemplateParams[index].Value);
        }
        public EditTemplateJson EditTemplateThird { get; set; } = new EditTemplateJson("!");

        public override UserControl GetUserControl()
        {
            return EditTemplateThird;
        }


        public override void Load()
        {
            SaveIndex.Clear();
            var backup = TemplateParams.ToDictionary(tp => tp.Id, tp => tp);

            if (MySqlSetting.Instance.IsUseMySql && MySqlSetting.IsConnect)
            {
                var smus = ModThirdPartyAlgorithmsDao.Instance.GetAllByPid(ThirdPartyAlgorithmsModel.Id);
                foreach (var dbModel in smus)
                {
                    if (dbModel != null)
                    {
                        if (Activator.CreateInstance(typeof(TemplateJsonParam), [dbModel]) is TemplateJsonParam t)
                        {
                            if (backup.TryGetValue(t.Id, out var model))
                            {
                                model.Value = t;
                                model.Key = t.Name;
                            }
                            else
                            {
                                var templateModel = new TemplateModel<TemplateJsonParam>(dbModel.Name ?? "default", t);
                                TemplateParams.Add(templateModel);
                            }
                        }
                    }
                }
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
                    ModThirdPartyAlgorithmsDao.Instance.Save(item.Value.ModThirdPartyAlgorithmsModel);
                }
            }
        }

        public override void Delete(int index)
        {
            int selectedCount = TemplateParams.Count(item => item.IsSelected);
            if (selectedCount == 1) index = TemplateParams.IndexOf(TemplateParams.First(item => item.IsSelected));

            void DeleteSingle(int id)
            {
                ModThirdPartyAlgorithmsDao.Instance.DeleteById(id, false);
                TemplateParams.RemoveAt(index);
            }

            if (selectedCount <= 1)
            {
                int id = TemplateParams[index].Value.Id;
                DeleteSingle(id);
            }
            else
            {
                foreach (var item in TemplateParams.Where(item => item.IsSelected == true).ToList())
                {
                    DeleteSingle(item.Id);
                }
            }
        }
        public override void Create(string templateName)
        {
            ModThirdPartyAlgorithmsModel thirdPartyAlgorithmsModel = new ModThirdPartyAlgorithmsModel() { PId = ThirdPartyAlgorithmsModel.Id, Code = Code, Name = templateName, JsonVal = ThirdPartyAlgorithmsModel.DefaultCfg };

            ModThirdPartyAlgorithmsDao.Instance.Save(thirdPartyAlgorithmsModel);
            TemplateJsonParam templateFindDotsArrayParam = new TemplateJsonParam(thirdPartyAlgorithmsModel);
            TemplateModel<TemplateJsonParam> templateModel = new TemplateModel<TemplateJsonParam>(templateFindDotsArrayParam.Name, templateFindDotsArrayParam);
            TemplateParams.Add(templateModel);
        }
    }
}
