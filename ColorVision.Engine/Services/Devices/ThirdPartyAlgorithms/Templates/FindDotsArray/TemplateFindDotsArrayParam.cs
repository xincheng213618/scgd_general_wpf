using ColorVision.Engine.MySql;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao;
using ColorVision.Engine.Services.SysDictionary;
using ColorVision.Engine.Templates;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http.Headers;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates.FindDotsArray
{
    public class TemplateFindDotsArrayParam : ITemplate<FindDotsArrayParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<FindDotsArrayParam>> Params { get; set; } = new ObservableCollection<TemplateModel<FindDotsArrayParam>>();

        public TemplateFindDotsArrayParam()
        {
            Title = "FindDotsArrayParam算法设置";
            Code = "FindDotsArrayParam";
            TemplateParams = Params;
            IsUserControl = true;
        }

        public override void SetUserControlDataContext(int index)
        {
            EditTemplateThird.SetParam(TemplateParams[index].Value);
        }
        public EditTemplateThird EditTemplateThird { get; set; } = new EditTemplateThird();

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

                var  smus = ModThirdPartyAlgorithmsDao.Instance.GetAllByPid(1);
                foreach (var dbModel in smus)
                {
                    if (dbModel != null)
                    {
                        if (Activator.CreateInstance(typeof(FindDotsArrayParam), [dbModel]) is FindDotsArrayParam t)
                        {
                            if (backup.TryGetValue(t.Id, out var model))
                            {
                                model.Value = t;
                                model.Key = t.Name;
                            }
                            else
                            {
                                var templateModel = new TemplateModel<FindDotsArrayParam>(dbModel.Name ?? "default", t);
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
                ModThirdPartyAlgorithmsDao.Instance.DeleteById(id ,false);
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
            var mode = ThirdPartyAlgorithmsDao.Instance.GetById(1);
            ModThirdPartyAlgorithmsModel thirdPartyAlgorithmsModel = new ModThirdPartyAlgorithmsModel() { PId = 1, Code = Code ,Name  =templateName ,JsonVal = mode.DefaultCfg};

            ModThirdPartyAlgorithmsDao.Instance.Save(thirdPartyAlgorithmsModel);
            FindDotsArrayParam templateFindDotsArrayParam = new FindDotsArrayParam(thirdPartyAlgorithmsModel);
            TemplateModel<FindDotsArrayParam> templateModel = new TemplateModel<FindDotsArrayParam>(templateFindDotsArrayParam.Name, templateFindDotsArrayParam);
            Params.Add(templateModel);

        }
    }
}
