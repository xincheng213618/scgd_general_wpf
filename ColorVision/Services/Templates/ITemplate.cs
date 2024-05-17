#pragma warning disable CS8604
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.MySql;
using ColorVision.Services.Dao;
using ColorVision.Services.Templates.POI.Dao;
using ColorVision.UserSpace;
using NPOI.SS.Formula.Functions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace ColorVision.Services.Templates
{
    public interface IITemplateLoad
    {
        public virtual void Load() { }
    }

    public class ITemplate
    {
        public virtual IEnumerable ItemsSource { get; }

        public string Title { get; set; }

        public string Code { get; set; }
        public virtual int Count { get; }

        public virtual string GetTemplateName(int index)
        {
            throw new NotImplementedException();
        }

        public virtual object GetValue()
        {
            throw new NotImplementedException();
        }

        public virtual object CreateDefault()
        {
            throw new NotImplementedException();
        }


        public virtual object GetValue(int index)
        {
            throw new NotImplementedException();
        }

        public virtual string NewCreateFileName(string FileName)
        {
            throw new NotImplementedException();
        }

        public virtual void  Save()
        {

        }

        public bool IsSideHide { get; set; }

        public virtual void PreviewMouseDoubleClick(int index)
        {

        }

        public virtual void Load() { }

        public virtual void Delete(int index)
        {
        }

        public virtual void Create(string templateName)
        {

        }
        public bool IsUserControl { get; set; }

        public virtual UserControl GetUserControl()
        {
            throw new NotImplementedException();
        }

        public virtual void SetUserControlDataContext(int index)
        {
            throw new NotImplementedException();
        }
    }



    public class ITemplate<T> : ITemplate where T : ParamBase, new()
    {
        public ObservableCollection<TemplateModel<T>> TemplateParams { get; set; } = new ObservableCollection<TemplateModel<T>>();

        public override int Count => TemplateParams.Count;

        public override object GetValue() => TemplateParams;
        public override object GetValue(int index) => TemplateParams[index].Value;

        public override IEnumerable ItemsSource { get => TemplateParams; }

        public override string GetTemplateName(int index) => TemplateParams[index].Key;

        public T? CreateTemp { get; set; }

        public override object CreateDefault() 
        {
            SysDictionaryModModel mod = SysDictionaryModDao.Instance.GetByCode(Code, UserConfig.Instance.TenantId);
            if (mod != null)
            {
                List<ModDetailModel> list = new();
                List<SysDictionaryModDetaiModel> sysDic = SysDictionaryModDetailDao.Instance.GetAllByPid(mod.Id);
                foreach (var item in sysDic)
                {
                    list.Add(new ModDetailModel(item.Id, -1, item.DefaultValue) { Symbol = item.Symbol });
                }

                ModMasterModel modMaster = new ModMasterModel(Code, "", UserConfig.Instance.TenantId);

                CreateTemp = (T)Activator.CreateInstance(typeof(T), new object[] { modMaster, list });
            }
            return CreateTemp ?? new T();
        }

        public override string NewCreateFileName(string FileName)
        {
            List<string> Names = new();
            foreach (var item in TemplateParams)
            {
                Names.Add(item.Key);
            }
            for (int i = 1; i < 9999; i++)
            {
                if (!Names.Contains($"{FileName}{i}"))
                    return $"{FileName}{i}";
            }
            return FileName;
        }

        public override void Save()
        {
            foreach (var item in TemplateParams)
            {
                if (ModMasterDao.Instance.GetById(item.Value.Id) is ModMasterModel modMasterModel && modMasterModel.Pcode != null)
                {
                    modMasterModel.Name = item.Value.Name;
                    ModMasterDao modMasterDao = new(modMasterModel.Pcode);
                    modMasterDao.Save(modMasterModel);
                }
                List<ModDetailModel> list = new();
                item.Value.GetDetail(list);
                ModDetailDao.Instance.UpdateByPid(item.Value.Id, list);
            }
        }


        public override void Load() => LoadModParam(Code);

        public void LoadModParam(string ModeType)
        {
            TemplateParams.Clear();
            if (MySqlSetting.Instance.IsUseMySql && MySqlSetting.IsConnect)
            {
                ModMasterDao masterDao = new ModMasterDao(ModeType);

                List<ModMasterModel> smus = masterDao.GetAll(UserConfig.Instance.TenantId);
                foreach (var dbModel in smus)
                {
                    List<ModDetailModel> smuDetails = ModDetailDao.Instance.GetAllByPid(dbModel.Id);
                    foreach (var dbDetail in smuDetails)
                    {
                        dbDetail.ValueA = dbDetail?.ValueA?.Replace("\\r", "\r");
                    }
                    if (dbModel != null && smuDetails !=null)
                    {
                        TemplateParams.Add(new TemplateModel<T>(dbModel.Name ?? "default", (T)Activator.CreateInstance(typeof(T), new object[] { dbModel, smuDetails })));
                    }
                }
            }
        }

        public override void Delete(int index)
        {
            if (index >= 0 && index < TemplateParams.Count)
            {
                int id = TemplateParams[index].Value.Id;
                List<ModDetailModel> de = ModDetailDao.Instance.GetAllByPid(id);
                int ret = ModMasterDao.Instance.DeleteById(id);
                ModDetailDao.Instance.DeleteAllByPid(id);
                if (de != null && de.Count > 0)
                {
                    string[] codes = new string[de.Count];
                    int idx = 0;
                    foreach (ModDetailModel model in de)
                    {
                        string code = model.GetValueMD5();
                        codes[idx++] = code;
                    }
                    VSysResourceDao.Instance.DeleteInCodes(codes);
                }
                TemplateParams.RemoveAt(index);
            }
        }



        public override void Create(string templateName)
        {
            T? AddParamMode()
            {
                ModMasterModel modMaster = new ModMasterModel(Code, templateName, UserConfig.Instance.TenantId);
                SysDictionaryModModel mod = SysDictionaryModDao.Instance.GetByCode(Code, UserConfig.Instance.TenantId);
                if (mod != null)
                {
                    modMaster.Pid = mod.Id;
                    ModMasterDao.Instance.Save(modMaster);
                    List<ModDetailModel> list = new();
                    if (CreateTemp != null)
                    {
                        CreateTemp.GetDetail(list);
                        foreach (var item in list)
                        {
                            item.Pid = modMaster.Id;
                        }
                    }
                    else
                    {
                        List<SysDictionaryModDetaiModel> sysDic = SysDictionaryModDetailDao.Instance.GetAllByPid(mod.Id);
                        foreach (var item in sysDic)
                        {
                            list.Add(new ModDetailModel(item.Id, modMaster.Id, item.DefaultValue));
                        }
                    }
                    ModDetailDao.Instance.SaveByPid(modMaster.Id, list);
                }
                if (modMaster.Id > 0)
                {
                    ModMasterModel modMasterModel = ModMasterDao.Instance.GetById(modMaster.Id);
                    List<ModDetailModel> modDetailModels = ModDetailDao.Instance.GetAllByPid(modMaster.Id);
                    if (modMasterModel != null)
                        return (T)Activator.CreateInstance(typeof(T), new object[] { modMasterModel, modDetailModels });
                }
                return null;
            }
            T? param = AddParamMode();
            if (param != null)
            {
                var a = new TemplateModel<T>(templateName, param);
                TemplateParams.Add(a);
            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"数据库创建{typeof(T)}模板失败", "ColorVision");
            }
        }
    }
}
