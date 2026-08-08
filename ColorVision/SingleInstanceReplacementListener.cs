using ColorVision.Update;
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
            TryRequestShutdown(processId, DefaultConnectTimeout);

        internal static SingleInstanceCloseRequestResult TryRequestShutdown(
            int processId,
            TimeSpan connectTimeout)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processId);
            ArgumentOutOfRangeException.ThrowIfLessThan(connectTimeout, TimeSpan.Zero);

            bool connected = false;
            try
            {
                using var pipe = new NamedPipeClientStream(
                    ".",
                    CreatePipeName(processId),
                    PipeDirection.In,
                    PipeOptions.None);
                pipe.Connect(checked((int)Math.Ceiling(connectTimeout.TotalMilliseconds)));
                connected = true;

                return pipe.ReadByte() switch
                {
                    AcceptedResponse => SingleInstanceCloseRequestResult.Accepted,
                    RejectedResponse => SingleInstanceCloseRequestResult.Rejected,
                    _ => SingleInstanceCloseRequestResult.Indeterminate,
                };
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
