using ColorVision.Database;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao;
using ColorVision.Engine.Templates;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates.Manager
{
    public class TemplateThirdPartyManager : ITemplate<ModThirdPartyManagerParam>
    {
        public static ObservableCollection<TemplateModel<ModThirdPartyManagerParam>> Params { get; set; } = new ObservableCollection<TemplateModel<ModThirdPartyManagerParam>>();


        public TemplateThirdPartyManager()
        {
            TemplateParams = Params;
            IsUserControl = true;
        }

        public override string Title { get => Code + ColorVision.Engine.Properties.Resources.Edit; set { } }

        public override void SetUserControlDataContext(int index)
        {
            EditTemplateThirdManager.SetParam(TemplateParams[index].Value);
        }
        public EditTemplateThirdManager EditTemplateThirdManager { get; set; } = new EditTemplateThirdManager();

        public override UserControl GetUserControl()
        {
            return EditTemplateThirdManager;
        }

        public int DLLId { get; set; } = -1;

        public override void Load()
        {
            SaveIndex.Clear();
            var backup = TemplateParams.ToDictionary(tp => tp.Id, tp => tp);

            if (MySqlSetting.Instance.IsUseMySql && MySqlSetting.IsConnect)
            {
                List<ThirdPartyAlgorithmsModel> models = new List<ThirdPartyAlgorithmsModel>();
                if (DLLId > 0)
                {
                    models = ThirdPartyAlgorithmsDao.Instance.GetAllByPid(DLLId);
                }
                else
                {
                    models = ThirdPartyAlgorithmsDao.Instance.GetAll();
                }

                foreach (var dbModel in models)
                {
                    if (dbModel != null)
                    {
                        if (Activator.CreateInstance(typeof(ModThirdPartyManagerParam), [dbModel]) is ModThirdPartyManagerParam t)
                        {
                            if (backup.TryGetValue(t.Id, out var model))
                            {
                                model.Value = t;
                                model.Key = t.Name;
                            }
                            else
                            {
                                var templateModel = new TemplateModel<ModThirdPartyManagerParam>(dbModel.Name ?? "default", t);
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
                    ThirdPartyAlgorithmsDao.Instance.Save(item.Value.ModThirdPartyAlgorithmsModel);
                }
            }
        }

        public override void Delete(int index)
        {
            int selectedCount = TemplateParams.Count(item => item.IsSelected);
            if (selectedCount == 1) index = TemplateParams.IndexOf(TemplateParams.First(item => item.IsSelected));

            void DeleteSingle(int id)
            {
                ThirdPartyAlgorithmsDao.Instance.DeleteById(id, false);
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
            ModThirdPartyManagerParam? AddParamMode()
            {
                ThirdPartyAlgorithmsModel thirdPartyAlgorithmsModel;
                if (CreateTemp != null)
                {
                    thirdPartyAlgorithmsModel = CreateTemp.ModThirdPartyAlgorithmsModel;
                    thirdPartyAlgorithmsModel.Id = 0;
                }
                else
                {
                   thirdPartyAlgorithmsModel = new ThirdPartyAlgorithmsModel() { Pid = DLLId, Code = templateName, Name = templateName };
                }
                ThirdPartyAlgorithmsDao.Instance.Save(thirdPartyAlgorithmsModel);
                if (thirdPartyAlgorithmsModel.Id > 0)
                    return new ModThirdPartyManagerParam(thirdPartyAlgorithmsModel);
                return null;
            }


            ModThirdPartyManagerParam? param = AddParamMode();
            if (param != null)
            {
                var a = new TemplateModel<ModThirdPartyManagerParam>(templateName, param);
                TemplateParams.Add(a);
            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"数据库创建{typeof(ModThirdPartyManagerParam)}模板失败", "ColorVision");
            }

        }
    }
}
