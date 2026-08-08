using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal delegate Task CopilotConversationTitleApplication(
        CopilotConversationRecord conversation,
        CopilotConversationTitleGenerationResult result,
        Func<bool> isCurrentGeneration,
        CancellationToken cancellationToken);

    internal sealed class CopilotConversationTitleCoordinator : IDisposable
    {
        private readonly object _gate = new();
        private readonly CopilotConversationTitleGenerator _generator;
        private readonly CopilotConversationTitleApplication _applyTitleAsync;
        private readonly Dictionary<string, CopilotNonBlockingCancellationSource> _generations = new(StringComparer.Ordinal);
        private bool _disposed;

        public CopilotConversationTitleCoordinator(
            CopilotConversationTitleGenerator generator,
            CopilotConversationTitleApplication applyTitleAsync)
        {
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
            _applyTitleAsync = applyTitleAsync ?? throw new ArgumentNullException(nameof(applyTitleAsync));
        }

        public Task QueueAsync(
            CopilotConversationRecord conversation,
            CopilotProfileConfig requestProfile)
        {
            ArgumentNullException.ThrowIfNull(conversation);
            ArgumentNullException.ThrowIfNull(requestProfile);

            if (!CopilotConversationTitleGenerator.TryCreateRequest(conversation, requestProfile, out var request))
                return Task.CompletedTask;

            var generation = new CopilotNonBlockingCancellationSource();
            CopilotNonBlockingCancellationSource? previousGeneration;
            lock (_gate)
            {
                if (_disposed)
                {
                    generation.Dispose();
                    return Task.CompletedTask;
                }

                _generations.Remove(conversation.Id, out previousGeneration);
                _generations[conversation.Id] = generation;
            }

            previousGeneration?.RequestCancellation();
            return GenerateAndApplyAsync(conversation, request, generation);
        }

        public void Cancel(string conversationId)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                return;

            CopilotNonBlockingCancellationSource? generation;
            lock (_gate)
                _generations.Remove(conversationId, out generation);

            generation?.RequestCancellation();
        }

        public void Dispose()
        {
            CopilotNonBlockingCancellationSource[] generations;
            lock (_gate)
            {
                if (_disposed)
                    return;

                _disposed = true;
                generations = _generations.Values.ToArray();
                _generations.Clear();
            }

            foreach (var generation in generations)
                generation.RequestCancellation();
        }

        private async Task GenerateAndApplyAsync(
            CopilotConversationRecord conversation,
            CopilotConversationTitleRequest request,
            CopilotNonBlockingCancellationSource generation)
        {
            try
            {
                var cancellationToken = generation.Token;
                var result = await _generator.GenerateAsync(request, cancellationToken).ConfigureAwait(false);
                if (IsDisposed())
                    return;

                await _applyTitleAsync(
                    conversation,
                    result,
                    () => IsCurrentGeneration(conversation.Id, generation),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (generation.IsCancellationRequested)
            {
            }
            catch
            {
            }
            finally
            {
                Complete(conversation.Id, generation);
            }
        }

        private bool IsCurrentGeneration(
            string conversationId,
            CopilotNonBlockingCancellationSource generation)
        {
            lock (_gate)
            {
                return !_disposed
                    && _generations.TryGetValue(conversationId, out var current)
                    && ReferenceEquals(current, generation);
            }
        }

        private bool IsDisposed()
        {
            lock (_gate)
                return _disposed;
        }

        private void Complete(
            string conversationId,
            CopilotNonBlockingCancellationSource generation)
        {
            lock (_gate)
            {
                if (_generations.TryGetValue(conversationId, out var current)
                    && ReferenceEquals(current, generation))
                {
                    _generations.Remove(conversationId);
                }
            }

            generation.Dispose();
        }
    }
}
