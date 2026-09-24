using ColorVision.UI;
using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.Startup;

internal static class MainWindowInitializerRunner
{
    private static readonly ILog log = LogManager.GetLogger(typeof(MainWindowInitializerRunner));

    internal static async Task<IReadOnlyList<StartupInitializerResult>> RunRequiredAsync(IEnumerable<IMainWindowInitialized> initializers)
    {
        var results = new List<StartupInitializerResult>();
        Stopwatch sliceStopwatch = Stopwatch.StartNew();
        foreach (IMainWindowInitialized initializer in initializers.Where(item => item is not IBackgroundMainWindowInitializer)
            .OrderBy(item => item.Order).ThenBy(item => item.Name, StringComparer.Ordinal))
        {
            if (sliceStopwatch.ElapsedMilliseconds >= 32)
            {
                await Dispatcher.Yield(DispatcherPriority.Background);
                sliceStopwatch.Restart();
            }
            StartupRegistryChecker.MarkStage("MainWindowInitializer", initializer.Name);
            results.Add(await RunAsync(initializer));
        }
        return results;
    }

    internal static async Task RunBackgroundAsync(IEnumerable<IMainWindowInitialized> initializers)
    {
        // Preserve each initializer's UI affinity while observing every exception.
        // Independent optional tasks do not form a serial dependency on one another.
        var tasks = new List<Task<StartupInitializerResult>>();
        try
        {
            foreach (IMainWindowInitialized initializer in initializers.OfType<IBackgroundMainWindowInitializer>())
            {
                if (Application.Current.Dispatcher.HasShutdownStarted)
                    break;
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                if (Application.Current.Dispatcher.HasShutdownStarted)
                    break;
                tasks.Add(RunAsync(initializer));
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { log.Warn("Unable to schedule optional startup work.", ex); }
        await Task.WhenAll(tasks);
    }

    private static async Task<StartupInitializerResult> RunAsync(IMainWindowInitialized initializer)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        Exception? error = null;
        try { await initializer.Initialize(); }
        catch (Exception ex)
        {
            error = ex;
            log.Error($"Main-window initializer {initializer.Name} failed.", ex);
        }
        var result = new StartupInitializerResult(initializer.Name, stopwatch.Elapsed, error);
        if (result.Duration.TotalMilliseconds >= 100)
            log.Info($"Slow main-window initializer {initializer.Name} completed in {result.Duration.TotalMilliseconds:0} ms. Success={result.Succeeded}.");
        return result;
    }
}
