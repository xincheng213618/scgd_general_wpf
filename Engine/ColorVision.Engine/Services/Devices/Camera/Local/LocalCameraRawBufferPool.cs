using ColorVision.Engine.FlowProcessing.Diagnostics;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    /// <summary>Optionally retains at most one idle RAW buffer for an open camera session.</summary>
    internal sealed class LocalCameraRawBufferPool : IDisposable
    {
        private static readonly object settingsSync = new();
        private static readonly List<WeakReference<LocalCameraRawBufferPool>> pools = new();
        private static bool isCacheEnabled;
        private readonly object sync = new();
        private IntPtr idlePointer;
        private int currentLength;
        private bool disposed;
        private bool cacheEnabled;

        internal LocalCameraRawBufferPool()
        {
            lock (settingsSync)
            {
                cacheEnabled = isCacheEnabled;
                pools.RemoveAll(reference => !reference.TryGetTarget(out _));
                pools.Add(new WeakReference<LocalCameraRawBufferPool>(this));
            }
        }

        internal static bool IsCacheEnabled
        {
            get { lock (settingsSync) return isCacheEnabled; }
            set
            {
                // Serialize policy changes with pool creation. Weak references do not prolong session ownership.
                lock (settingsSync)
                {
                    isCacheEnabled = value;
                    for (int index = pools.Count - 1; index >= 0; index--)
                    {
                        if (pools[index].TryGetTarget(out var pool)) pool.SetCacheEnabled(value);
                        else pools.RemoveAt(index);
                    }
                }
            }
        }

        private void SetCacheEnabled(bool enabled)
        {
            IntPtr pointer = IntPtr.Zero;
            lock (sync)
            {
                cacheEnabled = enabled;
                if (!enabled)
                {
                    pointer = idlePointer;
                    idlePointer = IntPtr.Zero;
                }
            }
            if (pointer != IntPtr.Zero) Marshal.FreeHGlobal(pointer);
        }

        internal int IdleBytes
        {
            get { lock (sync) return idlePointer == IntPtr.Zero ? 0 : currentLength; }
        }

        internal IntPtr Rent(int length)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
            IntPtr previous;
            bool matches;
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                previous = idlePointer;
                idlePointer = IntPtr.Zero;
                matches = currentLength == length;
                currentLength = length;
            }
            if (previous != IntPtr.Zero)
            {
                if (matches)
                {
                    using var reuse = FlowNodeTiming.Measure("ReuseRawBuffer");
                    reuse?.Complete();
                    return previous;
                }
                Marshal.FreeHGlobal(previous);
            }
            // A flow may retain several images until it finishes; never wait for a return.
            using var allocation = FlowNodeTiming.Measure("AllocateRawBuffer");
            IntPtr allocated = Marshal.AllocHGlobal(length);
            allocation?.Complete();
            return allocated;
        }

        internal void Return(IntPtr pointer, int length)
        {
            if (pointer == IntPtr.Zero) return;
            lock (sync)
            {
                if (!disposed && cacheEnabled && length == currentLength && idlePointer == IntPtr.Zero)
                {
                    idlePointer = pointer;
                    return;
                }
            }
            Marshal.FreeHGlobal(pointer);
        }

        // Detach only the idle slot; checked-out frames retain their own buffers.
        internal int ReleaseIdle()
        {
            IntPtr pointer;
            int releasedBytes;
            lock (sync)
            {
                pointer = idlePointer;
                releasedBytes = pointer == IntPtr.Zero ? 0 : currentLength;
                idlePointer = IntPtr.Zero;
            }
            if (pointer != IntPtr.Zero) Marshal.FreeHGlobal(pointer);
            return releasedBytes;
        }

        public void Dispose()
        {
            lock (sync) disposed = true;
            ReleaseIdle();
            GC.SuppressFinalize(this);
        }

        ~LocalCameraRawBufferPool() => Dispose();
    }
}
