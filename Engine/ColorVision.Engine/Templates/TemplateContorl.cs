#pragma warning disable CS1998
using ColorVision.Database;
using ColorVision.UI;
using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            MySqlControl.GetInstance().MySqlConnectChanged += (s, e) =>
                Application.Current.Dispatcher.Invoke(Init);
        }

        private static void Init()
        {
            if (!MySqlControl.GetInstance().IsConnect) return;
            Stopwatch totalStopwatch = Stopwatch.StartNew();
            Stopwatch phaseStopwatch = Stopwatch.StartNew();
            List<IITemplateLoad> templateLoaders = AssemblyHandler.GetInstance().LoadImplementations<IITemplateLoad>();
            long discoveryMilliseconds = phaseStopwatch.ElapsedMilliseconds;
            List<(string Name, long Milliseconds)> loaderTimings = new(templateLoaders.Count);
            foreach (var templateLoader in templateLoaders)
            {
                phaseStopwatch.Restart();
                try
                {
                    templateLoader.Load();
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
                finally
                {
                    loaderTimings.Add((templateLoader.GetType().Name, phaseStopwatch.ElapsedMilliseconds));
                }
            }

            totalStopwatch.Stop();
            string slowestLoaders = string.Join(", ", loaderTimings
                .OrderByDescending(timing => timing.Milliseconds)
                .Take(10)
                .Select(timing => $"{timing.Name}={timing.Milliseconds}ms"));
            log.Info($"Template initialization completed. Loaders={templateLoaders.Count}, " +
                $"Discovery={discoveryMilliseconds}ms, Load={loaderTimings.Sum(timing => timing.Milliseconds)}ms, " +
                $"Total={totalStopwatch.ElapsedMilliseconds}ms, Slowest=[{slowestLoaders}].");
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
