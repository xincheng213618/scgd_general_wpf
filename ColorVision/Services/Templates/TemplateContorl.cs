#pragma warning disable CS8604
using ColorVision.MySql;
using ColorVision.Services.Dao;
using ColorVision.UserSpace;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;

namespace ColorVision.Services.Templates
{
    /// <summary>
    /// 模板管理
    /// </summary>

    public class TemplateControl
    {
        private static TemplateControl _instance;
        private static readonly object _locker = new();
        public static TemplateControl GetInstance() { lock (_locker) { return _instance ??= new TemplateControl(); } }

        public TemplateControl()
        {
            MySqlSetting.Instance.UseMySqlChanged += (s) =>
            {
                Thread thread = new(async () =>
                {
                    if (!MySqlControl.GetInstance().IsConnect)
                        await MySqlControl.GetInstance().Connect();

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Init();
                    });
                });
                thread.Start();
            };
            Init();
            Application.Current.MainWindow.Closed += (s, e) =>
            {
                if (MySqlSetting.Instance.IsUseMySql)
                    return;
            };
        }
        private static void Init()
        {
            foreach (var type in Assembly.GetExecutingAssembly().GetTypes().Where(t => typeof(IITemplateLoad).IsAssignableFrom(t) && !t.IsAbstract))
            {
                if (Activator.CreateInstance(type) is IITemplateLoad iITemplateLoad)
                {
                    iITemplateLoad.Load();
                }
            }
        }


        public static void Save2DB<T>(ObservableCollection<TemplateModel<T>> keyValuePairs) where T : ParamBase
        {
            foreach (var item in keyValuePairs)
            {
                Save2DB(item.Value);
            }
        }

        public static void Save2DB<T>(T value) where T : ParamBase
        {
            if (ModMasterDao.Instance.GetById(value.Id) is ModMasterModel modMasterModel && modMasterModel.Pcode != null)
            {
                modMasterModel.Name = value.Name;
                ModMasterDao modMasterDao = new(modMasterModel.Pcode);
                modMasterDao.Save(modMasterModel);
            }
            List<ModDetailModel> list = new();
            value.GetDetail(list);
            ModDetailDao.Instance.UpdateByPid(value.Id, list);
        }


        public static void LoadModParam<T>(ObservableCollection<TemplateModel<T>> ParamModes, string ModeType) where T : ParamBase, new()
        {
            ParamModes.Clear();
            if (MySqlSetting.Instance.IsUseMySql && MySqlSetting.IsConnect)
            {
                ModMasterDao masterFlowDao = new(ModeType);

                List<ModMasterModel> smus = masterFlowDao.GetAll(UserConfig.Instance.TenantId);
                foreach (var dbModel in smus)
                {
                    List<ModDetailModel> smuDetails = ModDetailDao.Instance.GetAllByPid(dbModel.Id);
                    foreach (var dbDetail in smuDetails)
                    {
                        dbDetail.ValueA = dbDetail?.ValueA?.Replace("\\r", "\r");
                    }
                    ParamModes.Add(new TemplateModel<T>(dbModel.Name ?? "default", (T)Activator.CreateInstance(typeof(T), new object[] { dbModel, smuDetails })));
                }
            }
        }

        public static T? AddParamMode<T>(string code, string Name, int resourceId =-1) where T : ParamBase, new()
        {
            ModMasterModel modMaster = new(code, Name, UserConfig.Instance.TenantId);
            if (resourceId>0)
                modMaster.ResourceId = resourceId;
            Save(modMaster);
            int pkId = modMaster.Id;
            if (pkId > 0)
            {
                ModMasterModel modMasterModel = ModMasterDao.Instance.GetById(pkId);
                List<ModDetailModel> modDetailModels = ModDetailDao.Instance.GetAllByPid(pkId);
                if (modMasterModel != null) return (T)Activator.CreateInstance(typeof(T), new object[] { modMasterModel, modDetailModels });
                else return null;
            }
            return null;
        }


        public static int Save(ModMasterModel modMaster)
        {
            int ret = -1;
            SysDictionaryModModel mod = SysDictionaryModDao.Instance.GetByCode(modMaster.Pcode, modMaster.TenantId);
            if (mod != null)
            {
                modMaster.Pid = mod.Id;
                ret = ModMasterDao.Instance.Save(modMaster);
                List<ModDetailModel> list = new();
                List<SysDictionaryModDetaiModel> sysDic = SysDictionaryModDetailDao.Instance.GetAllByPid(modMaster.Pid);
                foreach (var item in sysDic)
                {
                    list.Add(new ModDetailModel(item.Id, modMaster.Id, item.DefaultValue));
                }
                ModDetailDao.Instance.SaveByPid(modMaster.Id, list);
            }
            return ret;
        }
    }
}
