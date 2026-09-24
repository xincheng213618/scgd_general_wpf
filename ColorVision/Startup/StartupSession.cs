using ColorVision.UI;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Startup;

internal enum StartupReadiness { Starting, FirstFrame, Ready, Degraded }

/// <summary>Owned by the host; rendering alone never completes required initialization.</summary>
internal sealed class StartupSession
{
    private readonly List<StartupInitializerResult> results = [];
    private bool firstFrame;
    private bool initializationCompleted;
    private bool completionPublished;

    public StartupReadiness Readiness => !initializationCompleted || !firstFrame
        ? firstFrame ? StartupReadiness.FirstFrame : StartupReadiness.Starting
        : results.All(result => result.Succeeded) ? StartupReadiness.Ready : StartupReadiness.Degraded;
    public IReadOnlyList<StartupInitializerResult> Results => results.ToArray();

    public void AddResults(IEnumerable<StartupInitializerResult> completed) => results.AddRange(completed);
    public void MarkFirstFrame() => firstFrame = true;
    public void MarkInitializationCompleted() => initializationCompleted = true;
    public bool TryPublishCompletion()
    {
        if (completionPublished || !firstFrame || !initializationCompleted)
            return false;
        completionPublished = true;
        return true;
    }
}
