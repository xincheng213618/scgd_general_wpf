#pragma warning disable CS8604
using ColorVision.Engine.MySql;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace ColorVision.Engine.Templates
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
            Init();
            MySqlControl.GetInstance().MySqlConnectChanged += (s, e) => Init();
        }

        private static async void Init()
        {
            if (!MySqlControl.GetInstance().IsConnect) return;
            await Task.Delay(10);
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
