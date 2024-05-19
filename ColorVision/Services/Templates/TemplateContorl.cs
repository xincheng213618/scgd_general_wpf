#pragma warning disable CS8604
using ColorVision.Common.MVVM;
using ColorVision.MySql;
using ColorVision.Services.Dao;
using ColorVision.UI;
using ColorVision.UI.Sorts;
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

    public class TemplateConfig : ViewModelBase, IConfig
    {
        public static TemplateConfig Instance => ConfigHandler.GetInstance().GetRequiredService<TemplateConfig>();

        public string DefaultCreateTemplateName { get => _DefaultCreateTemplateName; set { _DefaultCreateTemplateName = value; NotifyPropertyChanged(); } }
        private string _DefaultCreateTemplateName = ColorVision.Properties.Resource.DefaultCreateTemplateName;

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();
    }



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


        public static T? AddParamMode<T>(string code, string Name, int resourceId =-1) where T : ParamBase, new()
        {
            ModMasterModel modMaster = new ModMasterModel(code, Name, UserConfig.Instance.TenantId);
            if (resourceId > 0)
                modMaster.ResourceId = resourceId;
            SysDictionaryModModel mod = SysDictionaryModDao.Instance.GetByCode(code, UserConfig.Instance.TenantId);
            if (mod != null)
            {
                modMaster.Pid = mod.Id;
                ModMasterDao.Instance.Save(modMaster);
                List<ModDetailModel> list = new();
                List<SysDictionaryModDetaiModel> sysDic = SysDictionaryModDetailDao.Instance.GetAllByPid(mod.Id);
                foreach (var item in sysDic)
                {
                    list.Add(new ModDetailModel(item.Id, modMaster.Id, item.DefaultValue));
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
    }
}
