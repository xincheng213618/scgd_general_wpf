using System.Diagnostics;

namespace ColorVision.UI;

public sealed record StartupInitializerResult(string Name, TimeSpan Duration, Exception? Error)
{
    public bool Succeeded => Error == null;
}

/// <summary>Runs declared dependencies concurrently and preserves legacy serial boundaries.</summary>
public static class StartupInitializerRunner
{
    public static async Task<IReadOnlyList<StartupInitializerResult>> RunAsync(
        IReadOnlyList<IInitializer> initializers,
        Action<IInitializer>? starting = null,
        Func<IInitializer, StartupInitializerResult, Task>? completed = null,
        CancellationToken cancellationToken = default)
    {
        // Validate the complete plan before any initializer can have side effects.
        var names = initializers.Select(item => item.Name).ToHashSet(StringComparer.Ordinal);
        if (names.Count != initializers.Count)
            throw new InvalidOperationException("Startup initializer names must be unique.");
        var prerequisites = new Dictionary<IInitializer, HashSet<IInitializer>>();
        var byName = initializers.ToDictionary(item => item.Name, StringComparer.Ordinal);
        IInitializer? lastBarrier = null;
        var preceding = new List<IInitializer>();
        foreach (IInitializer initializer in initializers)
        {
            var dependencies = new HashSet<IInitializer>();
            if (initializer is IInitializerDependencies declared)
            {
                foreach (string name in declared.Dependencies)
                    if (byName.TryGetValue(name, out IInitializer? dependency))
                        dependencies.Add(dependency);
                if (lastBarrier != null)
                    dependencies.Add(lastBarrier);
            }
            else
            {
                dependencies.UnionWith(preceding);
                lastBarrier = initializer;
            }
            prerequisites.Add(initializer, dependencies);
            preceding.Add(initializer);
        }

        var remaining = new HashSet<IInitializer>(initializers);
        var ordered = new List<IInitializer>();
        while (remaining.Count != 0)
        {
            IInitializer[] ready = initializers.Where(item => remaining.Contains(item)
                && !prerequisites[item].Any(remaining.Contains)).ToArray();
            if (ready.Length == 0)
                throw new InvalidOperationException("Cyclic startup dependencies: " + string.Join(", ", remaining.Select(item => item.Name)));
            ordered.AddRange(ready);
            remaining.ExceptWith(ready);
        }

        using var completionGate = new SemaphoreSlim(1);
        var tasks = new Dictionary<IInitializer, Task<StartupInitializerResult>>();
        foreach (IInitializer initializer in ordered)
        {
            Task<StartupInitializerResult>[] dependencies = prerequisites[initializer].Select(item => tasks[item]).ToArray();
            tasks.Add(initializer, ExecuteAsync(initializer, dependencies));
        }
        // Observe all workers, including when cancellation or a host callback fails.
        await Task.WhenAll(tasks.Values).ConfigureAwait(false);
        return initializers.Select(item => tasks[item].Result).ToArray();

        async Task<StartupInitializerResult> ExecuteAsync(IInitializer initializer, Task<StartupInitializerResult>[] dependencies)
        {
            await Task.WhenAll(dependencies).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return await Task.Run(async () =>
            {
                starting?.Invoke(initializer);
                Stopwatch stopwatch = Stopwatch.StartNew();
                Exception? error = null;
                try { await initializer.InitializeAsync().ConfigureAwait(false); }
                catch (Exception ex) { error = ex; }
                var result = new StartupInitializerResult(initializer.Name, stopwatch.Elapsed, error);
                if (completed != null)
                {
                    await completionGate.WaitAsync().ConfigureAwait(false);
                    try { await completed(initializer, result).ConfigureAwait(false); }
                    finally { completionGate.Release(); }
                }
                return result;
            }, cancellationToken).ConfigureAwait(false);
        }
    }
}
