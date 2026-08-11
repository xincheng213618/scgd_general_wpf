using System.Collections.Concurrent;

namespace ColorVision.UI.Tests
{
    public sealed class ConfigReloadCoordinatorConcurrencyTests
    {
        private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

        [Fact]
        public async Task UnregisterWaitsForTheActiveBindAndPreventsFutureCallbacks()
        {
            var coordinator = new ConfigReloadCoordinator(new StubConfigService());
            var participant = new BlockingParticipant();
            coordinator.Register(participant);

            Task<ConfigReloadResult> bindTask = Task.Run(coordinator.BindCurrentConfigs);
            await participant.FirstBindEntered.WaitAsync(TestTimeout);
            var unregisterStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<bool> unregisterTask = Task.Run(() =>
            {
                unregisterStarted.TrySetResult();
                return coordinator.Unregister(participant);
            });

            try
            {
                await unregisterStarted.Task.WaitAsync(TestTimeout);
                await Task.Delay(100);
                Assert.False(unregisterTask.IsCompleted);
            }
            finally
            {
                participant.ReleaseFirstBind();
            }

            Assert.True(await unregisterTask.WaitAsync(TestTimeout));
            Assert.True((await bindTask.WaitAsync(TestTimeout)).Succeeded);
            Assert.Equal(1, participant.BindCount);

            ConfigReloadResult laterResult = coordinator.BindCurrentConfigs();
            Assert.True(laterResult.Succeeded);
            Assert.Equal(0, laterResult.AttemptedParticipantCount);
            Assert.Equal(1, participant.BindCount);
        }

        [Fact]
        public async Task UnregisterBarrierAlsoCoversMetadataGettersFromAnOldSnapshot()
        {
            var coordinator = new ConfigReloadCoordinator(new StubConfigService());
            var participant = new BlockingMetadataParticipant();
            coordinator.Register(participant);

            Task<ConfigReloadResult> bindTask = Task.Run(coordinator.BindCurrentConfigs);
            await participant.NameGetterEntered.WaitAsync(TestTimeout);
            Task<bool> unregisterTask = Task.Run(() => coordinator.Unregister(participant));

            try
            {
                await Task.Delay(100);
                Assert.False(unregisterTask.IsCompleted);
            }
            finally
            {
                participant.ReleaseNameGetter();
            }

            Assert.True(await unregisterTask.WaitAsync(TestTimeout));
            Assert.True((await bindTask.WaitAsync(TestTimeout)).Succeeded);
            Assert.Equal(0, participant.BindCount);
            Assert.Equal(0, coordinator.BindCurrentConfigs().AttemptedParticipantCount);
        }

        [Fact]
        public async Task ConcurrentReloadsNeverBindTheSameParticipantAtTheSameTime()
        {
            var coordinator = new ConfigReloadCoordinator(new StubConfigService());
            var participant = new BlockingParticipant();
            coordinator.Register(participant);

            Task<ConfigReloadResult> firstBind = Task.Run(coordinator.BindCurrentConfigs);
            await participant.FirstBindEntered.WaitAsync(TestTimeout);
            var secondBindStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<ConfigReloadResult> secondBind = Task.Run(() =>
            {
                secondBindStarted.TrySetResult();
                return coordinator.BindCurrentConfigs();
            });

            try
            {
                await secondBindStarted.Task.WaitAsync(TestTimeout);
                await Task.Delay(100);
                Assert.Equal(1, participant.BindCount);
                Assert.Equal(1, participant.MaximumConcurrentBindCount);
            }
            finally
            {
                participant.ReleaseFirstBind();
            }

            Assert.True((await firstBind.WaitAsync(TestTimeout)).Succeeded);
            Assert.True((await secondBind.WaitAsync(TestTimeout)).Succeeded);
            Assert.Equal(2, participant.BindCount);
            Assert.Equal(1, participant.MaximumConcurrentBindCount);
        }

        [Fact]
        public void RegisterAndBindBindsOnlyReferencesAddedByThatCall()
        {
            var coordinator = new ConfigReloadCoordinator(new StubConfigService());
            var existing = new RecordingParticipant("existing", 10);
            var added = new RecordingParticipant("added", 20);
            coordinator.Register(existing);

            ConfigReloadResult result = coordinator.RegisterAndBind(existing, added, existing, added);

            Assert.True(result.Succeeded);
            Assert.Equal(1, result.AttemptedParticipantCount);
            Assert.Equal(0, existing.BindCount);
            Assert.Equal(1, added.BindCount);

            ConfigReloadResult repeated = coordinator.RegisterAndBind(existing, added);
            Assert.True(repeated.Succeeded);
            Assert.Equal(0, repeated.AttemptedParticipantCount);
            Assert.Equal(0, existing.BindCount);
            Assert.Equal(1, added.BindCount);
        }

        [Fact]
        public void MetadataGetterFailuresAreOutsideTheRegistryLockAndDoNotStopBinding()
        {
            var coordinator = new ConfigReloadCoordinator(new StubConfigService());
            var calls = new ConcurrentQueue<string>();
            var nameProbe = new RecordingParticipant("name-probe", 30);
            var orderProbe = new RecordingParticipant("order-probe", 40);
            var broken = new ThrowingMetadataParticipant(coordinator, nameProbe, orderProbe, calls);
            var later = new RecordingParticipant("later", 10, calls);
            coordinator.Register(broken);
            coordinator.Register(later);

            ConfigReloadResult result = coordinator.BindCurrentConfigs();

            Assert.False(result.Succeeded);
            Assert.Equal(2, result.AttemptedParticipantCount);
            Assert.Equal(["later", "broken"], calls);
            Assert.Equal(1, broken.BindCount);
            Assert.Equal(1, later.BindCount);
            Assert.Equal(0, nameProbe.BindCount);
            Assert.Equal(0, orderProbe.BindCount);
            string fallbackName = typeof(ThrowingMetadataParticipant).FullName!;
            Assert.Collection(
                result.Failures,
                failure =>
                {
                    Assert.Equal(ConfigReloadFailureKind.Participant, failure.Kind);
                    Assert.Equal(fallbackName, failure.OwnerName);
                    Assert.Same(broken.NameFailure, failure.Exception);
                },
                failure =>
                {
                    Assert.Equal(ConfigReloadFailureKind.Participant, failure.Kind);
                    Assert.Equal(fallbackName, failure.OwnerName);
                    Assert.Same(broken.OrderFailure, failure.Exception);
                });
        }

        [Fact]
        public void ResultCopiesFailuresAndExposesAReadOnlyCollection()
        {
            var failure = new ConfigReloadFailure(
                ConfigReloadFailureKind.Participant,
                "owner",
                new InvalidOperationException("failure"));
            var source = new List<ConfigReloadFailure> { failure };

            var result = new ConfigReloadResult(1, 0, source);
            source.Clear();

            Assert.Same(failure, Assert.Single(result.Failures));
            IList<ConfigReloadFailure> exposed = Assert.IsAssignableFrom<IList<ConfigReloadFailure>>(result.Failures);
            Assert.True(exposed.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => exposed.Clear());
            Assert.Same(failure, Assert.Single(result.Failures));
        }

        [Fact]
        public async Task SelfUnregisterFromBindFailsExplicitlyWithoutRemovingTheParticipant()
        {
            var coordinator = new ConfigReloadCoordinator(new StubConfigService());
            var participant = new SelfUnregisteringParticipant(coordinator);
            coordinator.Register(participant);

            ConfigReloadResult result = await Task.Run(coordinator.BindCurrentConfigs).WaitAsync(TestTimeout);

            ConfigReloadFailure failure = Assert.Single(result.Failures);
            var exception = Assert.IsType<InvalidOperationException>(failure.Exception);
            Assert.Contains("Unregister", exception.Message, StringComparison.Ordinal);
            Assert.Equal(1, result.AttemptedParticipantCount);
            Assert.Equal(1, participant.BindCount);

            Assert.True(coordinator.Unregister(participant));
            Assert.Equal(0, coordinator.BindCurrentConfigs().AttemptedParticipantCount);
        }

        [Fact]
        public async Task FlowedTaskSelfUnregisterFailsExplicitlyWithoutDeadlock()
        {
            var coordinator = new ConfigReloadCoordinator(new StubConfigService());
            var participant = new FlowedTaskSelfUnregisteringParticipant(coordinator);
            coordinator.Register(participant);

            ConfigReloadResult result = await Task.Run(coordinator.BindCurrentConfigs).WaitAsync(TestTimeout);

            ConfigReloadFailure failure = Assert.Single(result.Failures);
            var exception = Assert.IsType<InvalidOperationException>(failure.Exception);
            Assert.Contains("Unregister", exception.Message, StringComparison.Ordinal);
            Assert.Equal(1, participant.BindCount);
            Assert.True(coordinator.Unregister(participant));
        }

        [Fact]
        public async Task FlowedTaskRecursiveBindIsRejectedAndLaterParticipantStillBindsOnce()
        {
            var coordinator = new ConfigReloadCoordinator(new StubConfigService());
            var recursive = new FlowedTaskRecursiveParticipant(coordinator);
            var later = new RecordingParticipant("later", 10);
            coordinator.Register(recursive);
            coordinator.Register(later);

            ConfigReloadResult result = await Task.Run(coordinator.BindCurrentConfigs).WaitAsync(TestTimeout);

            ConfigReloadFailure failure = Assert.Single(result.Failures);
            var exception = Assert.IsType<InvalidOperationException>(failure.Exception);
            Assert.Contains("recursively", exception.Message, StringComparison.Ordinal);
            Assert.Equal(1, recursive.BindCount);
            Assert.Equal(1, later.BindCount);
        }

        [Fact]
        public async Task DelayedFlowedChildCanBindAfterTheParentExecutionHasFinished()
        {
            var coordinator = new ConfigReloadCoordinator(new StubConfigService());
            var participant = new DelayedFlowedBindParticipant(coordinator);
            coordinator.Register(participant);

            ConfigReloadResult parentResult = coordinator.BindCurrentConfigs();
            participant.ReleaseChild();
            ConfigReloadResult childResult = await participant.ChildResult.WaitAsync(TestTimeout);

            Assert.True(parentResult.Succeeded, parentResult.BuildFailureSummary());
            Assert.True(childResult.Succeeded, childResult.BuildFailureSummary());
            Assert.Equal(2, participant.BindCount);
        }

        private sealed class BlockingParticipant : IConfigReloadParticipant
        {
            private readonly TaskCompletionSource _firstBindEntered =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource _releaseFirstBind =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _activeBindCount;
            private int _bindCount;
            private int _maximumConcurrentBindCount;

            public string ConfigReloadName => nameof(BlockingParticipant);

            public int ConfigReloadOrder => 0;

            public Task FirstBindEntered => _firstBindEntered.Task;

            public int BindCount => Volatile.Read(ref _bindCount);

            public int MaximumConcurrentBindCount => Volatile.Read(ref _maximumConcurrentBindCount);

            public void BindCurrentConfig(IConfigService currentConfig)
            {
                int activeCount = Interlocked.Increment(ref _activeBindCount);
                UpdateMaximum(ref _maximumConcurrentBindCount, activeCount);
                int bindCount = Interlocked.Increment(ref _bindCount);
                try
                {
                    if (bindCount == 1)
                    {
                        _firstBindEntered.TrySetResult();
                        _releaseFirstBind.Task.GetAwaiter().GetResult();
                    }
                }
                finally
                {
                    Interlocked.Decrement(ref _activeBindCount);
                }
            }

            public void ReleaseFirstBind() => _releaseFirstBind.TrySetResult();

            private static void UpdateMaximum(ref int target, int value)
            {
                int observed;
                do
                {
                    observed = Volatile.Read(ref target);
                    if (observed >= value)
                        return;
                }
                while (Interlocked.CompareExchange(ref target, value, observed) != observed);
            }
        }

        private sealed class BlockingMetadataParticipant : IConfigReloadParticipant
        {
            private readonly TaskCompletionSource _nameGetterEntered =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource _releaseNameGetter =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _bindCount;

            public string ConfigReloadName
            {
                get
                {
                    _nameGetterEntered.TrySetResult();
                    _releaseNameGetter.Task.GetAwaiter().GetResult();
                    return nameof(BlockingMetadataParticipant);
                }
            }

            public int ConfigReloadOrder => 0;

            public Task NameGetterEntered => _nameGetterEntered.Task;

            public int BindCount => Volatile.Read(ref _bindCount);

            public void BindCurrentConfig(IConfigService currentConfig)
            {
                Interlocked.Increment(ref _bindCount);
            }

            public void ReleaseNameGetter() => _releaseNameGetter.TrySetResult();
        }

        private sealed class RecordingParticipant : IConfigReloadParticipant
        {
            private readonly ConcurrentQueue<string>? _calls;
            private int _bindCount;

            public RecordingParticipant(string name, int order, ConcurrentQueue<string>? calls = null)
            {
                ConfigReloadName = name;
                ConfigReloadOrder = order;
                _calls = calls;
            }

            public string ConfigReloadName { get; }

            public int ConfigReloadOrder { get; }

            public int BindCount => Volatile.Read(ref _bindCount);

            public void BindCurrentConfig(IConfigService currentConfig)
            {
                Interlocked.Increment(ref _bindCount);
                _calls?.Enqueue(ConfigReloadName);
            }
        }

        private sealed class ThrowingMetadataParticipant : IConfigReloadParticipant
        {
            private readonly ConfigReloadCoordinator _coordinator;
            private readonly IConfigReloadParticipant _nameProbe;
            private readonly IConfigReloadParticipant _orderProbe;
            private readonly ConcurrentQueue<string> _calls;
            private int _bindCount;

            public ThrowingMetadataParticipant(
                ConfigReloadCoordinator coordinator,
                IConfigReloadParticipant nameProbe,
                IConfigReloadParticipant orderProbe,
                ConcurrentQueue<string> calls)
            {
                _coordinator = coordinator;
                _nameProbe = nameProbe;
                _orderProbe = orderProbe;
                _calls = calls;
            }

            public InvalidOperationException NameFailure { get; } = new("name getter failure");

            public InvalidOperationException OrderFailure { get; } = new("order getter failure");

            public string ConfigReloadName
            {
                get
                {
                    RegisterFromAnotherThread(_nameProbe);
                    throw NameFailure;
                }
            }

            public int ConfigReloadOrder
            {
                get
                {
                    RegisterFromAnotherThread(_orderProbe);
                    throw OrderFailure;
                }
            }

            public int BindCount => Volatile.Read(ref _bindCount);

            public void BindCurrentConfig(IConfigService currentConfig)
            {
                Interlocked.Increment(ref _bindCount);
                _calls.Enqueue("broken");
            }

            private void RegisterFromAnotherThread(IConfigReloadParticipant participant)
            {
                Task registerTask = Task.Run(() => _coordinator.Register(participant));
                if (!registerTask.Wait(TestTimeout))
                    throw new TimeoutException("Participant metadata was evaluated while the coordinator registry lock was held.");
                registerTask.GetAwaiter().GetResult();
            }
        }

        private sealed class SelfUnregisteringParticipant : IConfigReloadParticipant
        {
            private readonly ConfigReloadCoordinator _coordinator;
            private int _bindCount;

            public SelfUnregisteringParticipant(ConfigReloadCoordinator coordinator)
            {
                _coordinator = coordinator;
            }

            public string ConfigReloadName => nameof(SelfUnregisteringParticipant);

            public int ConfigReloadOrder => 0;

            public int BindCount => Volatile.Read(ref _bindCount);

            public void BindCurrentConfig(IConfigService currentConfig)
            {
                Interlocked.Increment(ref _bindCount);
                _coordinator.Unregister(this);
            }
        }

        private sealed class FlowedTaskSelfUnregisteringParticipant : IConfigReloadParticipant
        {
            private readonly ConfigReloadCoordinator _coordinator;
            private int _bindCount;

            public FlowedTaskSelfUnregisteringParticipant(ConfigReloadCoordinator coordinator)
            {
                _coordinator = coordinator;
            }

            public string ConfigReloadName => nameof(FlowedTaskSelfUnregisteringParticipant);

            public int ConfigReloadOrder => 0;

            public int BindCount => Volatile.Read(ref _bindCount);

            public void BindCurrentConfig(IConfigService currentConfig)
            {
                Interlocked.Increment(ref _bindCount);
                Task.Run(() => _coordinator.Unregister(this)).GetAwaiter().GetResult();
            }
        }

        private sealed class FlowedTaskRecursiveParticipant : IConfigReloadParticipant
        {
            private readonly ConfigReloadCoordinator _coordinator;
            private int _bindCount;

            public FlowedTaskRecursiveParticipant(ConfigReloadCoordinator coordinator)
            {
                _coordinator = coordinator;
            }

            public string ConfigReloadName => nameof(FlowedTaskRecursiveParticipant);

            public int ConfigReloadOrder => 0;

            public int BindCount => Volatile.Read(ref _bindCount);

            public void BindCurrentConfig(IConfigService currentConfig)
            {
                Interlocked.Increment(ref _bindCount);
                Task.Run(_coordinator.BindCurrentConfigs).GetAwaiter().GetResult();
            }
        }

        private sealed class DelayedFlowedBindParticipant : IConfigReloadParticipant
        {
            private readonly ConfigReloadCoordinator _coordinator;
            private readonly TaskCompletionSource _releaseChild =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _bindCount;

            public DelayedFlowedBindParticipant(ConfigReloadCoordinator coordinator)
            {
                _coordinator = coordinator;
            }

            public string ConfigReloadName => nameof(DelayedFlowedBindParticipant);

            public int ConfigReloadOrder => 0;

            public int BindCount => Volatile.Read(ref _bindCount);

            public Task<ConfigReloadResult> ChildResult { get; private set; } =
                Task.FromResult(ConfigReloadResult.Empty);

            public void BindCurrentConfig(IConfigService currentConfig)
            {
                if (Interlocked.Increment(ref _bindCount) != 1)
                    return;

                ChildResult = Task.Run(async () =>
                {
                    await _releaseChild.Task;
                    return _coordinator.BindCurrentConfigs();
                });
            }

            public void ReleaseChild() => _releaseChild.TrySetResult();
        }

        private sealed class StubConfigService : IConfigService
        {
            public IConfig GetRequiredService(Type type) => throw new NotSupportedException();

            public T1 GetRequiredService<T1>() where T1 : IConfig => throw new NotSupportedException();

            public void SaveConfigs()
            {
            }

            public void LoadConfigs()
            {
            }

            public void Save<T1>() where T1 : IConfig
            {
            }
        }
    }
}
