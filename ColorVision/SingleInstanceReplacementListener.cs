using ColorVision.Update;
using log4net;
using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision
{
    internal sealed class SingleInstanceReplacementListener : IDisposable
    {
        private const byte RejectedResponse = 0;
        private const byte AcceptedResponse = 1;
        private const string PipeNamePrefix = "ColorVision.SingleInstanceReplacement.";
        private static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan DefaultResponseTimeout = TimeSpan.FromSeconds(15);
        private static readonly ILog Log = LogManager.GetLogger(typeof(SingleInstanceReplacementListener));

        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly Action _finalizeShutdown;
        private readonly string _pipeName;
        private readonly Func<bool> _tryClose;
        private int _isDisposed;

        public SingleInstanceReplacementListener(
            int processId,
            Func<bool> tryClose,
            Action finalizeShutdown)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processId);
            ArgumentNullException.ThrowIfNull(tryClose);
            ArgumentNullException.ThrowIfNull(finalizeShutdown);

            _pipeName = CreatePipeName(processId);
            _tryClose = tryClose;
            _finalizeShutdown = finalizeShutdown;
            CancellationToken cancellationToken = _cancellationTokenSource.Token;
            _ = Task.Run(() => ListenAsync(cancellationToken));
        }

        public static SingleInstanceCloseRequestResult TryRequestShutdown(int processId) =>
            TryRequestShutdownAsync(processId, DefaultConnectTimeout, DefaultResponseTimeout).GetAwaiter().GetResult();

        internal static async Task<SingleInstanceCloseRequestResult> TryRequestShutdownAsync(
            int processId,
            TimeSpan connectTimeout,
            TimeSpan responseTimeout)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processId);
            ArgumentOutOfRangeException.ThrowIfLessThan(connectTimeout, TimeSpan.Zero);
            ArgumentOutOfRangeException.ThrowIfLessThan(responseTimeout, TimeSpan.Zero);

            bool connected = false;
            try
            {
                using var pipe = new NamedPipeClientStream(
                    ".",
                    CreatePipeName(processId),
                    PipeDirection.In,
                    PipeOptions.Asynchronous);
                await pipe.ConnectAsync(checked((int)Math.Ceiling(connectTimeout.TotalMilliseconds))).ConfigureAwait(false);
                connected = true;

                using var responseCancellation = new CancellationTokenSource(responseTimeout);
                byte[] response = new byte[1];
                int bytesRead = await pipe.ReadAsync(response.AsMemory(), responseCancellation.Token).ConfigureAwait(false);
                return (bytesRead == 1 ? response[0] : -1) switch
                {
                    AcceptedResponse => SingleInstanceCloseRequestResult.Accepted,
                    RejectedResponse => SingleInstanceCloseRequestResult.Rejected,
                    _ => SingleInstanceCloseRequestResult.Indeterminate,
                };
            }
            catch (OperationCanceledException) when (connected)
            {
                Log.Warn($"Earlier ColorVision process {processId} did not answer the shutdown request within {responseTimeout.TotalSeconds:0.###} seconds.");
                return SingleInstanceCloseRequestResult.TimedOut;
            }
            catch (Exception ex) when (ex is IOException
                or TimeoutException
                or UnauthorizedAccessException)
            {
                return connected
                    ? SingleInstanceCloseRequestResult.Indeterminate
                    : SingleInstanceCloseRequestResult.Unavailable;
            }
        }

        private async Task ListenAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var pipe = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.Out,
                        maxNumberOfServerInstances: 1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                    await HandleConnectionAsync(pipe).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                }
                catch (IOException)
                {
                }
            }
        }

        private async Task HandleConnectionAsync(NamedPipeServerStream pipe)
        {
            bool accepted = TryInvoke(_tryClose);

            try
            {
                pipe.WriteByte(accepted ? AcceptedResponse : RejectedResponse);
                await pipe.FlushAsync().ConfigureAwait(false);
            }
            finally
            {
                if (accepted)
                    TryInvoke(_finalizeShutdown);
            }
        }

        private static bool TryInvoke(Func<bool> action)
        {
            try
            {
                return action();
            }
            catch
            {
                return false;
            }
        }

        private static void TryInvoke(Action action)
        {
            try
            {
                action();
            }
            catch
            {
            }
        }

        internal static string CreatePipeName(int processId) => PipeNamePrefix + processId;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
                return;

            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
