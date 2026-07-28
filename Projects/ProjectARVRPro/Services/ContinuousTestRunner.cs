using System.Diagnostics;

namespace ProjectARVRPro.Services
{
    internal readonly record struct ContinuousTestProgress(
        int CompletedRounds,
        int PassedRounds,
        int FailedRounds,
        TimeSpan Elapsed);

    internal static class ContinuousTestRunner
    {
        public static async Task RunAsync(
            Func<CancellationToken, Task<bool>> runRoundAsync,
            Action<ContinuousTestProgress> progressChanged,
            TimeSpan interval,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(runRoundAsync);
            ArgumentNullException.ThrowIfNull(progressChanged);
            ArgumentOutOfRangeException.ThrowIfLessThan(interval, TimeSpan.Zero);

            int completedRounds = 0;
            int passedRounds = 0;
            int failedRounds = 0;
            var stopwatch = Stopwatch.StartNew();

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                bool passed = await runRoundAsync(cancellationToken);

                completedRounds++;
                if (passed)
                    passedRounds++;
                else
                    failedRounds++;

                progressChanged(new ContinuousTestProgress(
                    completedRounds,
                    passedRounds,
                    failedRounds,
                    stopwatch.Elapsed));

                if (interval > TimeSpan.Zero)
                    await Task.Delay(interval, cancellationToken);
                else
                    await Task.Yield();
            }
        }
    }
}
