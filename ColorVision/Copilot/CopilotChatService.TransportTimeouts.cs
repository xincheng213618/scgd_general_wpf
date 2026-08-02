#pragma warning disable CA1822,CA1861
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotChatService
    {
        private async Task<HttpResponseMessage> SendResponseHeadersAsync(
            HttpRequestMessage request,
            CopilotProviderInactivityTimeouts inactivityTimeouts,
            CancellationToken cancellationToken)
        {
            using var timeoutCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCancellation.CancelAfter(inactivityTimeouts.FirstResponseTimeout);
            try
            {
                return await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeoutCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested
                    && timeoutCancellation.IsCancellationRequested)
            {
                throw new CopilotProviderInactivityException(
                    CopilotProviderInactivityPhase.FirstResponse,
                    inactivityTimeouts.FirstResponseTimeout);
            }
        }

        private async Task<string> ReadBoundedContentWithTimeoutAsync(
            HttpResponseMessage response,
            int maximumBytes,
            string contentLabel,
            TimeSpan timeout,
            CopilotProviderInactivityPhase phase,
            CopilotProviderInactivityTimeouts inactivityTimeouts,
            CancellationToken cancellationToken)
        {
            if (timeout <= TimeSpan.Zero)
                throw CreateInactivityTimeout(phase, inactivityTimeouts);

            using var timeoutCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCancellation.CancelAfter(timeout);
            using var cancellationRegistration = timeoutCancellation.Token.Register(
                static state => ((HttpResponseMessage)state!).Dispose(),
                response);
            try
            {
                return await CopilotBoundedHttpContentReader.ReadAsStringAsync(
                    response.Content,
                    maximumBytes,
                    contentLabel,
                    timeoutCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested
                    && timeoutCancellation.IsCancellationRequested)
            {
                throw CreateInactivityTimeout(phase, inactivityTimeouts);
            }
            catch (Exception exception)
                when ((exception is ObjectDisposedException or IOException or HttpRequestException)
                    && timeoutCancellation.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw CreateInactivityTimeout(phase, inactivityTimeouts);
            }
        }

        private async Task<Stream> ReadResponseStreamWithTimeoutAsync(
            HttpResponseMessage response,
            TimeSpan timeout,
            CopilotProviderInactivityPhase phase,
            CopilotProviderInactivityTimeouts inactivityTimeouts,
            CancellationToken cancellationToken)
        {
            if (timeout <= TimeSpan.Zero)
                throw CreateInactivityTimeout(phase, inactivityTimeouts);

            using var timeoutCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCancellation.CancelAfter(timeout);
            using var cancellationRegistration = timeoutCancellation.Token.Register(
                static state => ((HttpResponseMessage)state!).Dispose(),
                response);
            try
            {
                return await response.Content.ReadAsStreamAsync(
                    timeoutCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested
                    && timeoutCancellation.IsCancellationRequested)
            {
                throw CreateInactivityTimeout(phase, inactivityTimeouts);
            }
            catch (Exception exception)
                when ((exception is ObjectDisposedException or IOException or HttpRequestException)
                    && timeoutCancellation.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw CreateInactivityTimeout(phase, inactivityTimeouts);
            }
        }

        private async Task<string?> ReadProviderLineWithTimeoutAsync(
            CopilotBoundedTextLineReader reader,
            HttpResponseMessage response,
            TimeSpan timeout,
            CopilotProviderInactivityPhase phase,
            CopilotProviderInactivityTimeouts inactivityTimeouts,
            CancellationToken cancellationToken)
        {
            if (timeout <= TimeSpan.Zero)
                throw CreateInactivityTimeout(phase, inactivityTimeouts);

            using var timeoutCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCancellation.CancelAfter(timeout);
            using var cancellationRegistration = timeoutCancellation.Token.Register(
                static state => ((HttpResponseMessage)state!).Dispose(),
                response);
            try
            {
                return await reader.ReadLineAsync(
                    timeoutCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested
                    && timeoutCancellation.IsCancellationRequested)
            {
                throw CreateInactivityTimeout(phase, inactivityTimeouts);
            }
            catch (Exception exception)
                when ((exception is ObjectDisposedException or IOException)
                    && timeoutCancellation.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw CreateInactivityTimeout(phase, inactivityTimeouts);
            }
        }

        private CopilotProviderInactivityException CreateInactivityTimeout(
            CopilotProviderInactivityPhase phase,
            CopilotProviderInactivityTimeouts inactivityTimeouts)
        {
            return new CopilotProviderInactivityException(
                phase,
                inactivityTimeouts.GetTimeout(phase));
        }

        private static TimeSpan SubtractElapsed(TimeSpan timeout, TimeSpan elapsed)
        {
            return timeout > elapsed ? timeout - elapsed : TimeSpan.Zero;
        }
    }
}
