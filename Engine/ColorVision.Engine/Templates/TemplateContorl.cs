﻿using ColorVision.Engine.MySql;
using ColorVision.UI;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Templates
{
    public interface IITemplateLoad
    {
        public virtual void Load() { }
    }

    /// <summary>
    /// 对模板进行初始化
    /// </summary>
    public class TemplateInitializer : IInitializer
    {
        private readonly IMessageUpdater _messageUpdater;

        public TemplateInitializer(IMessageUpdater messageUpdater)
        {
            _messageUpdater = messageUpdater;
        }

        public int Order => 4;

        public async Task InitializeAsync()
        {
            _messageUpdater.Update("正在加载模板");
            await Task.Delay(0);
            Application.Current.Dispatcher.Invoke(() => TemplateControl.GetInstance());
        }
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
            Init();
            MySqlControl.GetInstance().MySqlConnectChanged += (s, e) => Init();
        }

        private static async void Init()
        {
            if (!MySqlControl.GetInstance().IsConnect) return;
            await Task.Delay(0);
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