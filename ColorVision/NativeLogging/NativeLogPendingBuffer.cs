using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;

namespace ColorVision.NativeLogging;

internal sealed class NativeLogPendingBuffer
{
    private readonly Channel<NativeLogDisplayEntry> _channel;
    private int _pendingCount;
    private long _droppedCount;

    public NativeLogPendingBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        Capacity = capacity;
        _channel = Channel.CreateBounded<NativeLogDisplayEntry>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
            },
            _ =>
            {
                Interlocked.Decrement(ref _pendingCount);
                Interlocked.Increment(ref _droppedCount);
            });
    }

    public int Capacity { get; }

    public int PendingCount => Math.Max(0, Volatile.Read(ref _pendingCount));

    public long DroppedCount => Math.Max(0, Interlocked.Read(ref _droppedCount));

    public bool Enqueue(NativeLogDisplayEntry entry)
    {
        long droppedBefore = DroppedCount;
        if (!_channel.Writer.TryWrite(entry))
        {
            Interlocked.Increment(ref _droppedCount);
            return false;
        }

        Interlocked.Increment(ref _pendingCount);
        return DroppedCount == droppedBefore;
    }

    public NativeLogDrainBatch Drain(int maxEntries)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxEntries, 1);

        List<NativeLogDisplayEntry>? entries = null;
        while ((entries?.Count ?? 0) < maxEntries && _channel.Reader.TryRead(out NativeLogDisplayEntry entry))
        {
            entries ??= new List<NativeLogDisplayEntry>(Math.Min(maxEntries, PendingCount));
            entries.Add(entry);
            Interlocked.Decrement(ref _pendingCount);
        }

        return new NativeLogDrainBatch(entries ?? [], PendingCount, DroppedCount);
    }

    public void Clear()
    {
        while (_channel.Reader.TryRead(out _))
        {
            Interlocked.Decrement(ref _pendingCount);
        }

        Interlocked.Exchange(ref _droppedCount, 0);
    }

    public NativeLogBufferSnapshot GetSnapshot()
    {
        return new NativeLogBufferSnapshot(PendingCount, DroppedCount);
    }
}

internal readonly record struct NativeLogDrainBatch(
    IReadOnlyList<NativeLogDisplayEntry> Entries,
    int RemainingCount,
    long DroppedCount);

internal readonly record struct NativeLogBufferSnapshot(int PendingCount, long DroppedCount);
