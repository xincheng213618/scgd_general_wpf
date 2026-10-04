using ColorVision.Engine.FlowProcessing.Diagnostics;
using System;
using System.Runtime.InteropServices;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    /// <summary>Retains at most one idle RAW buffer for an open camera session.</summary>
    internal sealed class LocalCameraRawBufferPool : IDisposable
    {
        private readonly object sync = new();
        private IntPtr idlePointer;
        private int currentLength;
        private bool disposed;

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
                if (!disposed && length == currentLength && idlePointer == IntPtr.Zero)
                {
                    idlePointer = pointer;
                    return;
                }
            }
            Marshal.FreeHGlobal(pointer);
        }

        public void Dispose()
        {
            IntPtr pointer;
            lock (sync)
            {
                disposed = true;
                pointer = idlePointer;
                idlePointer = IntPtr.Zero;
            }
            if (pointer != IntPtr.Zero) Marshal.FreeHGlobal(pointer);
            GC.SuppressFinalize(this);
        }

        ~LocalCameraRawBufferPool() => Dispose();
    }
}
