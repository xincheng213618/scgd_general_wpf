using ColorVision.Engine.FlowProcessing.Diagnostics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ColorVision.Engine.FlowProcessing;

internal sealed class FlowProgressHistoryCache
{
    private const int Capacity = 100;
    private readonly Dictionary<FlowIdentity, Entry> entries = new();
    private long useSequence;

    internal static FlowProgressHistoryCache Shared { get; } = new();

    internal Entry GetOrAdd(FlowIdentity identity)
    {
        // Template IDs and names can change independently of the stable key.
        FlowIdentity key = identity.FlowKey is string flowKey
            ? new FlowIdentity(0, flowKey, null)
            : identity;
        lock (entries)
        {
            if (!entries.TryGetValue(key, out Entry? entry))
            {
                if (entries.Count >= Capacity)
                    entries.Remove(entries.MinBy(item => item.Value.LastUsed).Key);
                entries.Add(key, entry = new Entry());
            }
            entry.LastUsed = ++useSequence;
            return entry;
        }
    }

    internal sealed class Entry
    {
        internal long LastUsed { get; set; }
        // Synchronize the shared flow duration and the initial background read.
        internal object SyncRoot { get; } = new();
        internal long? FlowElapsedMs { get; set; }
        internal Task? PendingRead { get; set; }
        // Zero means the query succeeded and found no history for that node.
        internal ConcurrentDictionary<string, long> NodeDurations { get; } = new(StringComparer.Ordinal);
    }
}
