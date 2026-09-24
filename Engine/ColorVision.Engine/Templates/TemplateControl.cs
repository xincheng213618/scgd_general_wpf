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

    public interface IAsyncTemplateLoad : IITemplateLoad
    {
        Task LoadAsync();
    }

    /// <summary>
    /// 对模板进行初始化
    /// </summary>
    public class TemplateInitializer : InitializerBase, IInitializerDependencies
    {
        public override int Order => 4;

        public override string Name => nameof(TemplateInitializer);
        public IReadOnlyCollection<string> Dependencies => [nameof(MySqlInitializer), "SolutionManagerInitializer", nameof(Services.RC.RCInitializer)];

        public override async Task InitializeAsync()
        {
            await Application.Current.Dispatcher.InvokeAsync(TemplateControl.InitializeForStartupAsync).Task.Unwrap();
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

        private Task? reloadTask;
        private bool reloadRequested;

        public TemplateControl() : this(initialize: true) { }

        private TemplateControl(bool initialize)
        {
            if (initialize)
                InitializeTemplatesAsync(cooperative: false).GetAwaiter().GetResult();
            MySqlControl.GetInstance().MySqlConnectChanged += (_, _) =>
            {
                // Runtime reconnect consumers depend on template publication completing
                // before the subsequent service-hierarchy event handlers run.
                if (reloadTask is { IsCompleted: false }) reloadRequested = true;
                else InitializeTemplatesAsync(cooperative: false).GetAwaiter().GetResult();
            };
        }

        internal static Task InitializeForStartupAsync()
        {
            Application.Current.Dispatcher.VerifyAccess();
            lock (_locker)
            {
                if (_instance != null)
                    return _instance.reloadTask ?? Task.CompletedTask;
                _instance = new TemplateControl(initialize: false);
            }
            return _instance.ReloadAsync();
        }

        private Task ReloadAsync()
        {
            Application.Current.Dispatcher.VerifyAccess();
            if (reloadTask is { IsCompleted: false })
            {
                reloadRequested = true;
                return reloadTask;
            }
            return reloadTask = ReloadCoreAsync();
        }

        private async Task ReloadCoreAsync()
        {
            do
            {
                reloadRequested = false;
                await InitializeTemplatesAsync(cooperative: true);
            } while (reloadRequested);
        }

        private static async Task InitializeTemplatesAsync(bool cooperative)
        {
            if (!MySqlControl.GetInstance().IsConnect)
            {
                // Only initialize owners with local persistence; legacy template loaders still require MySQL.
                try
                {
                    var flow = new Flow.TemplateFlow();
                    if (cooperative) await flow.LoadAsync();
                    else flow.Load();
                }
                catch (Exception ex) { log.Error("Local flow template initialization failed.", ex); }
                return;
            }
            Stopwatch totalStopwatch = Stopwatch.StartNew();
            Stopwatch phaseStopwatch = Stopwatch.StartNew();
            List<IITemplateLoad> templateLoaders = AssemblyHandler.GetInstance().LoadImplementations<IITemplateLoad>();
            long discoveryMilliseconds = phaseStopwatch.ElapsedMilliseconds;
            List<(string Name, long Milliseconds)> loaderTimings = new(templateLoaders.Count);
            Stopwatch sliceStopwatch = Stopwatch.StartNew();
            foreach (var templateLoader in templateLoaders)
            {
                if (cooperative && sliceStopwatch.ElapsedMilliseconds >= 32)
                {
                    await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
                    sliceStopwatch.Restart();
                }
                phaseStopwatch.Restart();
                try
                {
                    if (cooperative && templateLoader is IAsyncTemplateLoad asyncLoader)
                        await asyncLoader.LoadAsync();
                    else
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
