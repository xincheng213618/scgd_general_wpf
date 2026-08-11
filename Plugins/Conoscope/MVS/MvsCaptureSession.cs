using System;
using System.Threading;

namespace Conoscope.MVS
{
    internal readonly record struct MvsCaptureStartResult(bool Started, bool AlreadyRunning, int NativeResult);

    internal readonly record struct MvsCaptureStopResult(bool WorkerExited, bool StopRequested, int NativeResult);

    /// <summary>
    /// Owns one capture generation at a time and keeps native start/stop ordering
    /// independent from the window's UI lifetime.
    /// </summary>
    internal sealed class MvsCaptureSession
    {
        private readonly object gate = new();
        private readonly object stopGate = new();
        private readonly Func<int> startGrabbing;
        private readonly Func<int> stopGrabbing;
        private readonly Action<CancellationToken> receiveLoop;
        private readonly Action<Exception> reportWorkerFailure;
        private readonly Action<Action> scheduleOwnerAction;
        private readonly Action<Exception, MvsCaptureStopResult> notifyOwnerFault;
        private readonly Func<Thread?, TimeSpan, bool> waitForThread;
        private readonly TimeSpan stopTimeout;

        private Thread? receiveThread;
        private CancellationTokenSource? stopSource;
        private bool nativeGrabbing;
        private bool stopInProgress;
        private long nextGenerationId;
        private long activeGenerationId;

        public MvsCaptureSession(
            Func<int> startGrabbing,
            Func<int> stopGrabbing,
            Action<CancellationToken> receiveLoop,
            Action<Exception> reportWorkerFailure,
            TimeSpan stopTimeout,
            Action<Action>? scheduleOwnerAction = null,
            Action<Exception, MvsCaptureStopResult>? notifyOwnerFault = null,
            Func<Thread?, TimeSpan, bool>? waitForThread = null)
        {
            this.startGrabbing = startGrabbing ?? throw new ArgumentNullException(nameof(startGrabbing));
            this.stopGrabbing = stopGrabbing ?? throw new ArgumentNullException(nameof(stopGrabbing));
            this.receiveLoop = receiveLoop ?? throw new ArgumentNullException(nameof(receiveLoop));
            this.reportWorkerFailure = reportWorkerFailure ?? throw new ArgumentNullException(nameof(reportWorkerFailure));
            this.scheduleOwnerAction = scheduleOwnerAction ?? (action => ThreadPool.QueueUserWorkItem(_ => action()));
            this.notifyOwnerFault = notifyOwnerFault ?? ((_, _) => { });
            this.waitForThread = waitForThread ?? WaitForThreadCore;
            this.stopTimeout = stopTimeout;
        }

        public bool IsWorkerAlive
        {
            get
            {
                lock (gate)
                {
                    return receiveThread?.IsAlive == true;
                }
            }
        }

        public MvsCaptureStartResult Start()
        {
            lock (gate)
            {
                if (stopInProgress)
                {
                    return new MvsCaptureStartResult(false, true, 0);
                }

                ClearExitedWorker();
                if (nativeGrabbing || receiveThread?.IsAlive == true)
                {
                    return new MvsCaptureStartResult(false, true, 0);
                }

                int nativeResult = startGrabbing();
                if (nativeResult != 0)
                {
                    return new MvsCaptureStartResult(false, false, nativeResult);
                }

                CancellationTokenSource? generationStopSource = null;
                long generationId = Interlocked.Increment(ref nextGenerationId);
                try
                {
                    generationStopSource = new CancellationTokenSource();
                    Thread generationThread = new(() => RunReceiveLoop(generationId, generationStopSource.Token))
                    {
                        IsBackground = true,
                        Name = "MVSFrameReceiver"
                    };

                    stopSource = generationStopSource;
                    receiveThread = generationThread;
                    nativeGrabbing = true;
                    activeGenerationId = generationId;
                    generationThread.Start();
                    return new MvsCaptureStartResult(true, false, nativeResult);
                }
                catch
                {
                    nativeGrabbing = false;
                    generationStopSource?.Cancel();
                    try
                    {
                        stopGrabbing();
                    }
                    finally
                    {
                        stopSource = null;
                        receiveThread = null;
                        activeGenerationId = 0;
                        generationStopSource?.Dispose();
                    }
                    throw;
                }
            }
        }

        public MvsCaptureStopResult Stop()
        {
            StopGeneration(null, out MvsCaptureStopResult result);
            return result;
        }

        private bool StopGeneration(long? expectedGenerationId, out MvsCaptureStopResult result)
        {
            lock (stopGate)
            {
                Thread? generationThread;
                CancellationTokenSource? generationStopSource;
                bool shouldStopNative;

                lock (gate)
                {
                    if (expectedGenerationId.HasValue && activeGenerationId != expectedGenerationId.Value)
                    {
                        result = default;
                        return false;
                    }

                    stopInProgress = true;
                    generationThread = receiveThread;
                    generationStopSource = stopSource;
                    shouldStopNative = nativeGrabbing;
                    nativeGrabbing = false;
                    generationStopSource?.Cancel();
                }

                int nativeResult = 0;
                bool workerExited = false;
                try
                {
                    nativeResult = shouldStopNative ? stopGrabbing() : 0;
                }
                finally
                {
                    workerExited = waitForThread(generationThread, stopTimeout);
                    lock (gate)
                    {
                        if (workerExited)
                        {
                            ClearWorkerCore(generationThread, generationStopSource);
                        }

                        stopInProgress = false;
                    }
                }

                result = new MvsCaptureStopResult(workerExited, shouldStopNative, nativeResult);
                return true;
            }
        }

        public bool WaitForExit(TimeSpan timeout)
        {
            Thread? generationThread;
            CancellationTokenSource? generationStopSource;
            lock (gate)
            {
                generationThread = receiveThread;
                generationStopSource = stopSource;
            }

            bool workerExited = waitForThread(generationThread, timeout);
            if (workerExited)
            {
                ClearWorker(generationThread, generationStopSource);
            }

            return workerExited;
        }

        private void RunReceiveLoop(long generationId, CancellationToken cancellationToken)
        {
            try
            {
                receiveLoop(cancellationToken);
                if (!cancellationToken.IsCancellationRequested)
                {
                    ScheduleFaultStop(generationId, new InvalidOperationException("Capture receive loop exited unexpectedly."));
                }
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                ScheduleFaultStop(generationId, ex);
            }
        }

        private void ScheduleFaultStop(long generationId, Exception exception)
        {
            reportWorkerFailure(exception);
            scheduleOwnerAction(() =>
            {
                if (StopGeneration(generationId, out MvsCaptureStopResult stopResult))
                {
                    notifyOwnerFault(exception, stopResult);
                }
            });
        }

        private static bool WaitForThreadCore(Thread? thread, TimeSpan timeout)
        {
            if (thread == null || !thread.IsAlive)
            {
                return true;
            }

            if (ReferenceEquals(thread, Thread.CurrentThread))
            {
                return false;
            }

            if (timeout == Timeout.InfiniteTimeSpan)
            {
                thread.Join();
                return true;
            }

            return thread.Join(timeout);
        }

        private void ClearExitedWorker()
        {
            if (!nativeGrabbing && receiveThread?.IsAlive == false)
            {
                stopSource?.Dispose();
                stopSource = null;
                receiveThread = null;
                activeGenerationId = 0;
            }
        }

        private void ClearWorker(Thread? generationThread, CancellationTokenSource? generationStopSource)
        {
            lock (gate)
            {
                ClearWorkerCore(generationThread, generationStopSource);
            }
        }

        private void ClearWorkerCore(Thread? generationThread, CancellationTokenSource? generationStopSource)
        {
            if (!ReferenceEquals(receiveThread, generationThread))
            {
                return;
            }

            receiveThread = null;
            stopSource = null;
            activeGenerationId = 0;
            generationStopSource?.Dispose();
        }
    }

    internal sealed class MvsDeferredCleanup
    {
        private readonly Func<TimeSpan, bool> waitForExit;
        private readonly Action cleanup;
        private readonly Action<Exception> reportFailure;
        private int scheduled;

        public MvsDeferredCleanup(
            Func<TimeSpan, bool> waitForExit,
            Action cleanup,
            Action<Exception> reportFailure)
        {
            this.waitForExit = waitForExit ?? throw new ArgumentNullException(nameof(waitForExit));
            this.cleanup = cleanup ?? throw new ArgumentNullException(nameof(cleanup));
            this.reportFailure = reportFailure ?? throw new ArgumentNullException(nameof(reportFailure));
        }

        public void Schedule()
        {
            if (Interlocked.Exchange(ref scheduled, 1) != 0)
            {
                return;
            }

            Thread cleanupThread = new(() =>
            {
                try
                {
                    if (waitForExit(Timeout.InfiniteTimeSpan))
                    {
                        cleanup();
                    }
                }
                catch (Exception ex)
                {
                    reportFailure(ex);
                }
            })
            {
                IsBackground = true,
                Name = "MVSDeferredCleanup"
            };
            cleanupThread.Start();
        }
    }
}
