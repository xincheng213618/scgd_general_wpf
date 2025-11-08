#pragma warning disable CS1998
using ColorVision.Database;
using ColorVision.UI;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
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
    public class TemplateInitializer : InitializerBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(TemplateInitializer));

        public override int Order => 4;

        public override string Name => nameof(TemplateInitializer);

        public override IEnumerable<string> Dependencies => new List<string>() { nameof(MySqlInitializer) };

        public override async Task InitializeAsync()
        {
            log.Info(ColorVision.Engine.Properties.Resources.LoadingTempate);
            Application.Current.Dispatcher.Invoke(() => TemplateControl.GetInstance());
        }
    }


    /// <summary>
    /// 模板管理
    /// </summary>

    public class TemplateControl
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(TemplateControl));

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
            foreach (var assembly in Application.Current.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes().Where(t => typeof(IITemplateLoad).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    try
                    {
                        if (Activator.CreateInstance(type) is IITemplateLoad iITemplateLoad)
                        {
                            iITemplateLoad.Load();
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex);
                    }
                }
            }
        }

        public static Dictionary<string, ITemplate> ITemplateNames { get; set; } = new Dictionary<string, ITemplate>();

        public static void AddITemplateInstance(string code, ITemplate templateName)
        {
            if (!ITemplateNames.TryAdd(code, templateName))
            {
                ITemplateNames[code] = templateName;
            }
        }

        public static bool ExitsTemplateName(string templateName)
        {
            var templateNames = ITemplateNames.Values
               .SelectMany(item => item.GetTemplateNames())
               .Distinct()
               .ToList();
            return templateNames.Any(a => a.Equals(templateName, StringComparison.OrdinalIgnoreCase));
        }
        public static ITemplate? FindDuplicateTemplate(string templateName)
        {
            var duplicates = ITemplateNames.Values
                .FirstOrDefault(item => item.GetTemplateNames()
                    .Any(name => name.Equals(templateName, StringComparison.OrdinalIgnoreCase)));

            return duplicates;
        }
    }
}
