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
using System.Windows.Threading;

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
        private static readonly HashSet<Type> DeferredLoaderTypes =
        [
            typeof(POI.TemplatePoi),
            typeof(Flow.TemplateFlow),
            typeof(POI.POIOutput.TemplatePoiOutputParam),
        ];
        private static readonly List<IITemplateLoad> DeferredLoaders = new();

        private static TemplateControl _instance;
        private static readonly object _locker = new();
        public static TemplateControl GetInstance() { lock (_locker) { return _instance ??= new TemplateControl(); } }

        public TemplateControl()
        {
            Init(deferHeavyLoaders: true);
            MySqlControl.GetInstance().MySqlConnectChanged += (s, e) =>
                Application.Current.Dispatcher.Invoke(() => Init(deferHeavyLoaders: false));
        }

        private static void Init(bool deferHeavyLoaders)
        {
            if (!MySqlControl.GetInstance().IsConnect) return;
            Stopwatch totalStopwatch = Stopwatch.StartNew();
            Stopwatch phaseStopwatch = Stopwatch.StartNew();
            List<IITemplateLoad> templateLoaders = AssemblyHandler.GetInstance().LoadImplementations<IITemplateLoad>();
            long discoveryMilliseconds = phaseStopwatch.ElapsedMilliseconds;
            List<(string Name, long Milliseconds)> loaderTimings = new(templateLoaders.Count);
            DeferredLoaders.Clear();
            foreach (var templateLoader in templateLoaders)
            {
                if (deferHeavyLoaders && DeferredLoaderTypes.Contains(templateLoader.GetType()))
                {
                    DeferredLoaders.Add(templateLoader);
                    continue;
                }

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
                $"Deferred={DeferredLoaders.Count}, " +
                $"Discovery={discoveryMilliseconds}ms, Load={loaderTimings.Sum(timing => timing.Milliseconds)}ms, " +
                $"Total={totalStopwatch.ElapsedMilliseconds}ms, Slowest=[{slowestLoaders}].");
        }

        internal static void LoadDeferredTemplates()
        {
            if (DeferredLoaders.Count == 0)
                return;

            Stopwatch totalStopwatch = Stopwatch.StartNew();
            List<(string Name, long Milliseconds)> loaderTimings = new(DeferredLoaders.Count);
            foreach (IITemplateLoad templateLoader in DeferredLoaders.ToArray())
            {
                Stopwatch loaderStopwatch = Stopwatch.StartNew();
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
                    loaderTimings.Add((templateLoader.GetType().Name, loaderStopwatch.ElapsedMilliseconds));
                }
            }
            DeferredLoaders.Clear();
            totalStopwatch.Stop();
            log.Info($"Deferred template initialization completed. " +
                $"Loaders={loaderTimings.Count}, Total={totalStopwatch.ElapsedMilliseconds}ms, " +
                $"Details=[{string.Join(", ", loaderTimings.Select(timing => $"{timing.Name}={timing.Milliseconds}ms"))}].");
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

    /// <summary>
    /// Loads the few expensive template collections after the main window has
    /// rendered, but before Flow and device display controls are materialized.
    /// </summary>
    public sealed class DeferredTemplateInitializer : MainWindowInitializedBase
    {
        public override int Order { get; set; } = -200;

        public override async Task Initialize()
        {
            await Application.Current.Dispatcher.InvokeAsync(
                TemplateControl.LoadDeferredTemplates,
                DispatcherPriority.ApplicationIdle);
        }
    }
}
