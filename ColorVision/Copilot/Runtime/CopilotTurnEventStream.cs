using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal static class CopilotTurnEventStream
    {
        public static async IAsyncEnumerable<CopilotTurnEvent> RunAsync(
            Func<CopilotTurnEventSink, CancellationToken, Task<CopilotTurnResult>> runTurn,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(runTurn);

            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var channel = Channel.CreateUnbounded<CopilotTurnEvent>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
            });
            var sink = new CopilotTurnEventSink(turnEvent => channel.Writer.TryWrite(turnEvent));
            var producer = ProduceAsync(runTurn, sink, channel.Writer, lifetime.Token);

            try
            {
                await foreach (var turnEvent in channel.Reader.ReadAllAsync(lifetime.Token).ConfigureAwait(false))
                    yield return turnEvent;
            }
            finally
            {
                lifetime.Cancel();
                await producer.ConfigureAwait(false);
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
    }
}
