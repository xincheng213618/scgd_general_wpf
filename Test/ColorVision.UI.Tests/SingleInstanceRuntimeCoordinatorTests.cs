namespace ColorVision.UI.Tests
{
    public sealed class SingleInstanceRuntimeCoordinatorTests
    {
        [Fact]
        public async Task SavesBeforeAndAfterClosingInstancesAndAcquiringMutex()
        {
            var calls = new List<string>();
            var coordinator = new SingleInstanceRuntimeCoordinator(
                () =>
                {
                    calls.Add("close");
                    return Task.FromResult(2);
                },
                () =>
                {
                    calls.Add("acquire");
                    return true;
                },
                () => calls.Add("save"));

            int? closedInstanceCount = await coordinator.EnforceSingleInstanceAsync();

            Assert.Equal(2, closedInstanceCount);
            Assert.Collection(
                calls,
                call => Assert.Equal("save", call),
                call => Assert.Equal("close", call),
                call => Assert.Equal("acquire", call),
                call => Assert.Equal("save", call));
        }

        [Fact]
        public async Task FailsWithoutSavingAgainWhenMutexCannotBeAcquired()
        {
            int saveCount = 0;
            var coordinator = new SingleInstanceRuntimeCoordinator(
                () => Task.FromResult(1),
                () => false,
                () => saveCount++);

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => coordinator.EnforceSingleInstanceAsync());

            Assert.Contains("single-instance mutex", exception.Message);
            Assert.Equal(1, saveCount);
        }

        [Fact]
        public async Task IgnoresASecondTransitionWhileTheFirstIsRunning()
        {
            var closeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var allowCloseToFinish = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var coordinator = new SingleInstanceRuntimeCoordinator(
                async () =>
                {
                    closeStarted.SetResult();
                    return await allowCloseToFinish.Task;
                },
                () => true,
                () => { });

            Task<int?> firstTransition = coordinator.EnforceSingleInstanceAsync();
            await closeStarted.Task;

            int? secondResult = await coordinator.EnforceSingleInstanceAsync();
            allowCloseToFinish.SetResult(3);
            int? firstResult = await firstTransition;

            Assert.Null(secondResult);
            Assert.Equal(3, firstResult);
        }
    }
}
