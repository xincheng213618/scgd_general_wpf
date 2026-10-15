using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.ImageEditor.Output
{
    public sealed record SnapshotBufferCacheEntry(long Id, string SourceName, long IdleBytes, long ActiveBytes,
        long PendingReleaseBytes, int ActiveCount, long HitCount, int IdleWidth, int IdleHeight, string PixelFormat, bool IsClosed);

    public sealed record SnapshotBufferReleaseResult(long ReleasedBytes, long PendingReleaseBytes);

    /// <summary>Process-local snapshot buffers. Weak registrations never keep an image view or its buffers alive.</summary>
    public static class SnapshotBufferCache
    {
        private static readonly object sync = new();
        private static readonly List<WeakReference<SnapshotImageBufferPool>> pools = new();
        internal static void Register(SnapshotImageBufferPool pool)
        {
            lock (sync)
            {
                pools.RemoveAll(reference => !reference.TryGetTarget(out _));
                pools.Add(new(pool));
            }
        }

        public static IReadOnlyList<SnapshotBufferCacheEntry> GetSnapshot()
            => ReadPools().Select(pool => pool.GetSnapshot()).Where(entry => !entry.IsClosed || entry.ActiveCount > 0).ToArray();

        /// <summary>Null selects all registered views; an ID selects exactly one view, even when its title is duplicated.</summary>
        public static SnapshotBufferReleaseResult Release(long? sourceId = null)
        {
            long released = 0;
            long pending = 0;
            foreach (SnapshotImageBufferPool pool in ReadPools().Where(pool => sourceId == null || pool.Id == sourceId))
            {
                SnapshotBufferReleaseResult result = pool.Release();
                released += result.ReleasedBytes;
                pending += result.PendingReleaseBytes;
            }
            return new(released, pending);
        }

        private static SnapshotImageBufferPool[] ReadPools()
        {
            lock (sync)
            {
                List<SnapshotImageBufferPool> live = new();
                pools.RemoveAll(reference =>
                {
                    if (!reference.TryGetTarget(out SnapshotImageBufferPool? pool)) return true;
                    live.Add(pool);
                    return false;
                });
                return live.ToArray();
            }
        }
    }
}
