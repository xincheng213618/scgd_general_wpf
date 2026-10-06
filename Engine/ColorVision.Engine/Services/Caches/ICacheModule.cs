using System.Collections.Generic;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Caches
{
    internal interface ICacheModule
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
        CacheModuleSnapshot GetSnapshot();
        Task<CacheModuleReleaseResult> ReleaseAsync();
        void SetEnabled(bool enabled);
    }

    internal sealed record CacheEntrySnapshot(
        string Name,
        string FilePath,
        ulong FileBytes,
        ulong MemoryBytes,
        ulong HitCount,
        ulong ActiveReferences,
        string State);

    internal sealed record CacheModuleSnapshot(
        string Id,
        string Name,
        string Description,
        ulong MemoryBytes,
        int EntryCount,
        ulong HitCount,
        ulong MissCount,
        ulong ActiveReferences,
        bool IsEnabled,
        bool CanToggle,
        string DetailSummary,
        IReadOnlyList<CacheEntrySnapshot> Entries,
        string? Error = null);

    internal sealed record CacheModuleReleaseResult(string ModuleId, ulong ReleasedBytes, string Message, bool Succeeded);
}
