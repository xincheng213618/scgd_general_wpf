using ColorVision.Engine.MySql;
using ColorVision.Engine.Rbac;
using ColorVision.Engine.Templates;
using ColorVision.Engine.ThirdPartyAlgorithms.Devices.ThirdPartyAlgorithms.Templates.FindDotsArray;
using NPOI.SS.Formula.Functions;
using NPOI.XWPF.UserModel;
using System.Collections.Generic;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao;
using ColorVision.Engine.MySql.ORM;

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
                        if (Activator.CreateInstance(typeof(T), [dbModel]) is FindDotsArrayParam t)
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


        public override void Create(string templateName)
        {
            ModThirdPartyAlgorithmsModel thirdPartyAlgorithmsModel = new ModThirdPartyAlgorithmsModel() { PId = 1, Code = Code };

            ModThirdPartyAlgorithmsDao.Instance.Save(thirdPartyAlgorithmsModel);
            FindDotsArrayParam templateFindDotsArrayParam = new FindDotsArrayParam(thirdPartyAlgorithmsModel);
            TemplateModel<FindDotsArrayParam> templateModel = new TemplateModel<FindDotsArrayParam>(templateFindDotsArrayParam.Name, templateFindDotsArrayParam);
            Params.Add(templateModel);

        }
    }
}
