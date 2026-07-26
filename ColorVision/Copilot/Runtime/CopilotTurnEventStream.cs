using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal static class CopilotTurnEventStream
    {
        internal static readonly TimeSpan DefaultProducerShutdownTimeout = TimeSpan.FromSeconds(7);

        public static async IAsyncEnumerable<CopilotTurnEvent> RunAsync(
            Func<CopilotTurnEventSink, CancellationToken, Task<CopilotTurnResult>> runTurn,
            [EnumeratorCancellation] CancellationToken cancellationToken,
            TimeSpan? producerShutdownTimeout = null)
        {
            ArgumentNullException.ThrowIfNull(runTurn);
            var shutdownTimeout = producerShutdownTimeout ?? DefaultProducerShutdownTimeout;
            if (shutdownTimeout <= TimeSpan.Zero || shutdownTimeout == Timeout.InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(producerShutdownTimeout), "Producer shutdown timeout must be finite and positive.");

            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            using var cancellationDrainGuard = new CancellationTokenSource();
            var channel = Channel.CreateUnbounded<CopilotTurnEvent>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
            });
            var sink = new CopilotTurnEventSink(turnEvent => channel.Writer.TryWrite(turnEvent));
            // Async delegates execute synchronously until their first incomplete await. Start the
            // entire turn on the thread pool so provider setup and extension code cannot occupy
            // the WPF thread before yielding.
            var producer = Task.Run(
                () => ProduceAsync(runTurn, sink, channel.Writer, lifetime.Token),
                CancellationToken.None);
            var cancellationDrain = EnforceCancellationDeadlineAsync(
                producer,
                channel.Writer,
                shutdownTimeout,
                cancellationToken,
                cancellationDrainGuard.Token);

            try
            {
                // Caller cancellation stops the producer, but the reader remains alive for a
                // bounded grace period so Agent pause/cancel can publish its final checkpoint
                // and structured completion event.
                await foreach (var turnEvent in channel.Reader.ReadAllAsync().ConfigureAwait(false))
                    yield return turnEvent;
            }
            finally
            {
                lifetime.Cancel();
                channel.Writer.TryComplete();
                cancellationDrainGuard.Cancel();
                var cancellationDrainOutcome = await cancellationDrain.ConfigureAwait(false);
                if (cancellationDrainOutcome == CancellationDrainOutcome.NotRequested
                    && !producer.IsCompleted)
                {
                    await WaitForProducerShutdownAsync(producer, shutdownTimeout).ConfigureAwait(false);
                }
            }
        }

        private static async Task<CancellationDrainOutcome> EnforceCancellationDeadlineAsync(
            Task producer,
            ChannelWriter<CopilotTurnEvent> writer,
            TimeSpan shutdownTimeout,
            CancellationToken callerCancellationToken,
            CancellationToken guardCancellationToken)
        {
            if (!callerCancellationToken.CanBeCanceled)
                return CancellationDrainOutcome.NotRequested;

            using var cancellationSignal = CancellationTokenSource.CreateLinkedTokenSource(
                callerCancellationToken,
                guardCancellationToken);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationSignal.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }

            if (!callerCancellationToken.IsCancellationRequested)
                return CancellationDrainOutcome.NotRequested;

            try
            {
                await producer.WaitAsync(shutdownTimeout).ConfigureAwait(false);
                return CancellationDrainOutcome.Completed;
            }
            catch (TimeoutException)
            {
                Trace.TraceWarning(
                    "Copilot turn producer did not stop within {0}; closing the event stream.",
                    shutdownTimeout);
                writer.TryComplete(new OperationCanceledException(
                    "Copilot turn producer did not stop within the cancellation grace period.",
                    callerCancellationToken));
                return CancellationDrainOutcome.TimedOut;
            }
        }

        private static async Task WaitForProducerShutdownAsync(Task producer, TimeSpan shutdownTimeout)
        {
            try
            {
                await producer.WaitAsync(shutdownTimeout).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                Trace.TraceWarning(
                    "Copilot turn producer ignored disposal cancellation for {0}; detaching it from the closed event stream.",
                    shutdownTimeout);
            }
        }

        private static async Task ProduceAsync(
            Func<CopilotTurnEventSink, CancellationToken, Task<CopilotTurnResult>> runTurn,
            CopilotTurnEventSink sink,
            ChannelWriter<CopilotTurnEvent> writer,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await runTurn(sink, cancellationToken).ConfigureAwait(false);
                if (!writer.TryWrite(new CopilotTurnCompletedEvent(result)))
                    throw new InvalidOperationException("Copilot turn event stream closed before completion.");
                writer.TryComplete();
            }
            catch (Exception ex)
            {
                writer.TryComplete(ex);
            }
        }

        private enum CancellationDrainOutcome
        {
            NotRequested,
            Completed,
            TimedOut,
        }
    }
}
