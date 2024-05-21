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
using System.Threading.Tasks;
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
            MySqlSetting.Instance.UseMySqlChanged += async (s) =>
            {
                await Task.Run(async () =>
                {
                    if (!MySqlControl.GetInstance().IsConnect)
                        await MySqlControl.GetInstance().Connect();

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        Init();
                    });
                });
            };
            Init();
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
    }
}
