using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;

namespace ColorVision.UI
{
    public enum ConfigReloadFailureKind
    {
        Participant,
        LegacySubscriber,
        SourceRead,
        SourceInstall,
    }

    public enum ConfigSourceReadStatus
    {
        NotAttempted,
        Succeeded,
        Missing,
        Invalid,
    }

    public enum ConfigRecoveryStatus
    {
        NotAttempted,
        NotRequired,
        RestoredBackup,
        LoadedDefaults,
    }

    public sealed class ConfigReloadFailure
    {
        internal ConfigReloadFailure(ConfigReloadFailureKind kind, string ownerName, Exception exception)
        {
            Kind = kind;
            OwnerName = ownerName;
            Exception = exception;
        }

        public ConfigReloadFailureKind Kind { get; }

        public string OwnerName { get; }

        public Exception Exception { get; }
    }

    public sealed class ConfigReloadResult
    {
        private readonly ReadOnlyCollection<ConfigReloadFailure> _failures;

        internal ConfigReloadResult(
            int attemptedParticipantCount,
            int attemptedLegacySubscriberCount,
            IReadOnlyList<ConfigReloadFailure> failures,
            ConfigSourceReadStatus sourceReadStatus = ConfigSourceReadStatus.NotAttempted,
            ConfigRecoveryStatus recoveryStatus = ConfigRecoveryStatus.NotAttempted)
        {
            ArgumentNullException.ThrowIfNull(failures);
            AttemptedParticipantCount = attemptedParticipantCount;
            AttemptedLegacySubscriberCount = attemptedLegacySubscriberCount;
            _failures = Array.AsReadOnly(failures.ToArray());
            SourceReadStatus = sourceReadStatus;
            RecoveryStatus = recoveryStatus;
        }

        public static ConfigReloadResult Empty { get; } = new(0, 0, Array.Empty<ConfigReloadFailure>());

        public int AttemptedParticipantCount { get; }

        public int AttemptedLegacySubscriberCount { get; }

        public ConfigSourceReadStatus SourceReadStatus { get; }

        public ConfigRecoveryStatus RecoveryStatus { get; }

        public IReadOnlyList<ConfigReloadFailure> Failures => _failures;

        public bool Succeeded => Failures.Count == 0;

        public AggregateException CreateAggregateException(string message = "One or more configuration reload operations failed.") =>
            new(message, Failures.Select(failure => failure.Exception));

        public void ThrowIfFailed(string message = "One or more configuration reload operations failed.")
        {
            if (!Succeeded)
                throw CreateAggregateException(message);
        }

        public string BuildFailureSummary(int maximumFailureCount = 8)
        {
            if (Succeeded)
                return string.Empty;

            int count = Math.Max(1, maximumFailureCount);
            var lines = Failures
                .Take(count)
                .Select(failure => $"{failure.OwnerName}: {NormalizeMessage(failure.Exception.Message)}")
                .ToList();
            if (Failures.Count > count)
                lines.Add($"... and {Failures.Count - count} more failure(s).");
            return string.Join(Environment.NewLine, lines);
        }

        internal ConfigReloadResult AppendLegacySubscriberResults(
            int attemptedSubscriberCount,
            IReadOnlyList<ConfigReloadFailure> subscriberFailures)
        {
            if (attemptedSubscriberCount == 0 && subscriberFailures.Count == 0)
                return this;

            return new ConfigReloadResult(
                AttemptedParticipantCount,
                AttemptedLegacySubscriberCount + attemptedSubscriberCount,
                Failures.Concat(subscriberFailures).ToArray(),
                SourceReadStatus,
                RecoveryStatus);
        }

        internal ConfigReloadResult WithSourceStatus(
            ConfigSourceReadStatus sourceReadStatus,
            ConfigRecoveryStatus recoveryStatus)
        {
            return new ConfigReloadResult(
                AttemptedParticipantCount,
                AttemptedLegacySubscriberCount,
                Failures,
                sourceReadStatus,
                recoveryStatus);
        }

        internal static ConfigReloadResult FromSource(
            ConfigSourceReadStatus sourceReadStatus,
            ConfigRecoveryStatus recoveryStatus,
            ConfigReloadFailure failure)
        {
            ArgumentNullException.ThrowIfNull(failure);
            return new ConfigReloadResult(0, 0, [failure], sourceReadStatus, recoveryStatus);
        }

        private static string NormalizeMessage(string? message)
        {
            return string.IsNullOrWhiteSpace(message)
                ? "Unknown error."
                : message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        }
    }

    /// <summary>
    /// Coordinates process-lifetime configuration owners independently of the legacy reload event.
    /// </summary>
    public sealed class ConfigReloadCoordinator
    {
        private readonly IConfigService _configService;
        private readonly object _syncRoot = new();
        private readonly object _bindingExecutionRoot = new();
        private readonly AsyncLocal<long?> _bindingExecutionContext = new();
        private readonly HashSet<long> _activeBindingExecutions = new();
        private readonly List<ParticipantRegistration> _participants = new();
        private readonly Dictionary<IConfigReloadParticipant, ParticipantRegistration> _retiringParticipants =
            new(ReferenceEqualityComparer.Instance);
        private long _nextBindingExecutionId;
        private long _nextSequence;

        public ConfigReloadCoordinator(IConfigService configService)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        }

        /// <summary>
        /// Registers a participant by reference. Registering the same object again is a no-op.
        /// </summary>
        public void Register(IConfigReloadParticipant participant)
        {
            ArgumentNullException.ThrowIfNull(participant);
            RegisterCore(participant);
        }

        /// <summary>
        /// Removes a participant and waits until all metadata and binding callbacks that already
        /// captured that registration have finished. Self-unregistration from a callback is rejected.
        /// </summary>
        public bool Unregister(IConfigReloadParticipant participant)
        {
            ArgumentNullException.ThrowIfNull(participant);

            ParticipantRegistration registration;
            bool removed;
            lock (_syncRoot)
            {
                int index = _participants.FindIndex(item => ReferenceEquals(item.Participant, participant));
                if (index < 0)
                {
                    if (!_retiringParticipants.TryGetValue(participant, out registration!))
                        return false;

                    if (registration.IsBindingInCurrentContext)
                        throw CreateSelfUnregisterException(participant);
                    removed = false;
                }
                else
                {
                    registration = _participants[index];
                    if (registration.IsBindingInCurrentContext)
                        throw CreateSelfUnregisterException(participant);

                    _participants.RemoveAt(index);
                    registration.Deactivate();
                    _retiringParticipants.Add(participant, registration);
                    removed = true;
                }
            }

            if (!removed)
            {
                registration.WaitForQuiescence();
                return false;
            }

            try
            {
                registration.WaitForQuiescence();
            }
            finally
            {
                lock (_syncRoot)
                {
                    if (_retiringParticipants.TryGetValue(participant, out ParticipantRegistration? retiring)
                        && ReferenceEquals(retiring, registration))
                    {
                        _retiringParticipants.Remove(participant);
                        System.Threading.Monitor.PulseAll(_syncRoot);
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Registers and initially binds only references that were newly added by this call.
        /// Existing registrations are not rebound.
        /// </summary>
        public ConfigReloadResult RegisterAndBind(params IConfigReloadParticipant[] participants)
        {
            ArgumentNullException.ThrowIfNull(participants);
            foreach (IConfigReloadParticipant participant in participants)
                ArgumentNullException.ThrowIfNull(participant);
            ThrowIfBindingIsReentrant();

            var registrations = new List<ParticipantRegistration>(participants.Length);
            foreach (IConfigReloadParticipant participant in participants)
            {
                ParticipantRegistration? registration = RegisterCore(participant);
                if (registration != null)
                    registrations.Add(registration);
            }

            return BindRegistrations(registrations.ToArray());
        }

        /// <summary>
        /// Binds a stable snapshot of all current registrations in configured order. Each
        /// participant is serialized against concurrent binds and isolated from other failures.
        /// </summary>
        public ConfigReloadResult BindCurrentConfigs()
        {
            ThrowIfBindingIsReentrant();
            ParticipantRegistration[] participants;
            lock (_syncRoot)
                participants = _participants.ToArray();

            return BindRegistrations(participants);
        }

        private ConfigReloadResult BindRegistrations(ParticipantRegistration[] participants)
        {
            long? inheritedExecutionId = _bindingExecutionContext.Value;
            long executionId;
            lock (_bindingExecutionRoot)
            {
                ThrowIfBindingIsReentrantNoLock(inheritedExecutionId);
                executionId = ++_nextBindingExecutionId;
                _activeBindingExecutions.Add(executionId);
            }

            _bindingExecutionContext.Value = executionId;
            try
            {
                var failures = new List<ConfigReloadFailure>();
                ParticipantBinding[] bindings = participants
                    .Select(registration => TryCreateBinding(registration, failures))
                    .OfType<ParticipantBinding>()
                    .OrderBy(binding => binding.Order)
                    .ThenBy(binding => binding.Registration.Sequence)
                    .ToArray();

                foreach (ParticipantBinding binding in bindings)
                {
                    bool entered;
                    ParticipantRegistration.BindingLease? lease = null;
                    try
                    {
                        entered = binding.Registration.TryEnterBind(out lease);
                    }
                    catch (Exception ex)
                    {
                        failures.Add(new ConfigReloadFailure(
                            ConfigReloadFailureKind.Participant,
                            binding.OwnerName,
                            ex));
                        continue;
                    }

                    if (!entered)
                        continue;

                    try
                    {
                        binding.Registration.Participant.BindCurrentConfig(_configService);
                    }
                    catch (Exception ex)
                    {
                        failures.Add(new ConfigReloadFailure(
                            ConfigReloadFailureKind.Participant,
                            binding.OwnerName,
                            ex));
                    }
                    finally
                    {
                        lease!.Dispose();
                    }
                }

                return new ConfigReloadResult(participants.Length, 0, failures);
            }
            finally
            {
                _bindingExecutionContext.Value = inheritedExecutionId;
                lock (_bindingExecutionRoot)
                    _activeBindingExecutions.Remove(executionId);
            }
        }

        private void ThrowIfBindingIsReentrant()
        {
            long? inheritedExecutionId = _bindingExecutionContext.Value;
            lock (_bindingExecutionRoot)
                ThrowIfBindingIsReentrantNoLock(inheritedExecutionId);
        }

        private void ThrowIfBindingIsReentrantNoLock(long? inheritedExecutionId)
        {
            if (inheritedExecutionId.HasValue && _activeBindingExecutions.Contains(inheritedExecutionId.Value))
                throw new InvalidOperationException(
                    "Configuration participants cannot be bound recursively from inside an active participant binding execution.");
        }

        private static ParticipantBinding? TryCreateBinding(
            ParticipantRegistration registration,
            List<ConfigReloadFailure> failures)
        {
            bool entered;
            ParticipantRegistration.BindingLease? lease = null;
            try
            {
                entered = registration.TryEnterBind(out lease);
            }
            catch (Exception ex)
            {
                failures.Add(new ConfigReloadFailure(
                    ConfigReloadFailureKind.Participant,
                    GetFallbackParticipantName(registration.Participant),
                    ex));
                return null;
            }

            if (!entered)
                return null;

            try
            {
                return CreateBinding(registration, failures);
            }
            finally
            {
                lease!.Dispose();
            }
        }

        private ParticipantRegistration? RegisterCore(IConfigReloadParticipant participant)
        {
            lock (_syncRoot)
            {
                while (_retiringParticipants.TryGetValue(participant, out ParticipantRegistration? retiring))
                {
                    if (retiring.IsBindingInCurrentContext)
                    {
                        throw new InvalidOperationException(
                            $"Cannot register {GetFallbackParticipantName(participant)} while its configuration callback is being unregistered.");
                    }

                    System.Threading.Monitor.Wait(_syncRoot);
                }

                if (_participants.Any(item => ReferenceEquals(item.Participant, participant)))
                    return null;

                var registration = new ParticipantRegistration(participant, _nextSequence++);
                _participants.Add(registration);
                return registration;
            }
        }

        private static ParticipantBinding CreateBinding(
            ParticipantRegistration registration,
            List<ConfigReloadFailure> failures)
        {
            IConfigReloadParticipant participant = registration.Participant;
            string ownerName = GetFallbackParticipantName(participant);
            try
            {
                string configuredName = participant.ConfigReloadName;
                if (!string.IsNullOrWhiteSpace(configuredName))
                    ownerName = configuredName;
            }
            catch (Exception ex)
            {
                failures.Add(new ConfigReloadFailure(
                    ConfigReloadFailureKind.Participant,
                    ownerName,
                    ex));
            }

            int order = int.MaxValue;
            try
            {
                order = participant.ConfigReloadOrder;
            }
            catch (Exception ex)
            {
                failures.Add(new ConfigReloadFailure(
                    ConfigReloadFailureKind.Participant,
                    ownerName,
                    ex));
            }

            return new ParticipantBinding(registration, ownerName, order);
        }

        private static InvalidOperationException CreateSelfUnregisterException(IConfigReloadParticipant participant) =>
            new($"{GetFallbackParticipantName(participant)} cannot unregister itself from inside BindCurrentConfig because Unregister must wait for that callback to finish.");

        private static string GetFallbackParticipantName(IConfigReloadParticipant participant) =>
            participant.GetType().FullName ?? participant.GetType().Name;

        private sealed record ParticipantBinding(
            ParticipantRegistration Registration,
            string OwnerName,
            int Order);

        private sealed class ParticipantRegistration
        {
            private readonly object _stateRoot = new();
            private readonly AsyncLocal<long?> _bindingContext = new();
            private bool _isRegistered = true;
            private bool _isBinding;
            private int _bindingThreadId;
            private long _activeBindingId;
            private long _nextBindingId;

            public ParticipantRegistration(IConfigReloadParticipant participant, long sequence)
            {
                Participant = participant;
                Sequence = sequence;
            }

            public IConfigReloadParticipant Participant { get; }

            public long Sequence { get; }

            public bool IsBindingInCurrentContext
            {
                get
                {
                    long? bindingContext = _bindingContext.Value;
                    lock (_stateRoot)
                    {
                        return _isBinding
                            && (_bindingThreadId == Environment.CurrentManagedThreadId
                                || bindingContext == _activeBindingId);
                    }
                }
            }

            public bool TryEnterBind(out BindingLease? lease)
            {
                lease = null;
                long? inheritedBindingContext = _bindingContext.Value;
                lock (_stateRoot)
                {
                    while (_isBinding)
                    {
                        if (_bindingThreadId == Environment.CurrentManagedThreadId
                            || inheritedBindingContext == _activeBindingId)
                        {
                            throw new InvalidOperationException(
                                $"{GetFallbackParticipantName(Participant)} cannot enter BindCurrentConfig recursively.");
                        }
                        if (!_isRegistered)
                            return false;

                        System.Threading.Monitor.Wait(_stateRoot);
                    }

                    if (!_isRegistered)
                        return false;

                    _isBinding = true;
                    _bindingThreadId = Environment.CurrentManagedThreadId;
                    _activeBindingId = ++_nextBindingId;
                    _bindingContext.Value = _activeBindingId;
                    lease = new BindingLease(this, _activeBindingId, inheritedBindingContext);
                    return true;
                }
            }

            private void ExitBind(long bindingId, long? inheritedBindingContext)
            {
                _bindingContext.Value = inheritedBindingContext;
                lock (_stateRoot)
                {
                    if (!_isBinding || _activeBindingId != bindingId)
                        throw new InvalidOperationException("The configuration binding scope is no longer active.");

                    _isBinding = false;
                    _bindingThreadId = 0;
                    _activeBindingId = 0;
                    System.Threading.Monitor.PulseAll(_stateRoot);
                }
            }

            public void Deactivate()
            {
                lock (_stateRoot)
                {
                    _isRegistered = false;
                    System.Threading.Monitor.PulseAll(_stateRoot);
                }
            }

            public void WaitForQuiescence()
            {
                long? bindingContext = _bindingContext.Value;
                lock (_stateRoot)
                {
                    if (_isBinding
                        && (_bindingThreadId == Environment.CurrentManagedThreadId
                            || bindingContext == _activeBindingId))
                    {
                        throw new InvalidOperationException(
                            $"Cannot wait for {GetFallbackParticipantName(Participant)} while executing its BindCurrentConfig callback.");
                    }

                    while (_isBinding)
                        System.Threading.Monitor.Wait(_stateRoot);
                }
            }

            public sealed class BindingLease : IDisposable
            {
                private ParticipantRegistration? _registration;
                private readonly long _bindingId;
                private readonly long? _inheritedBindingContext;

                public BindingLease(
                    ParticipantRegistration registration,
                    long bindingId,
                    long? inheritedBindingContext)
                {
                    _registration = registration;
                    _bindingId = bindingId;
                    _inheritedBindingContext = inheritedBindingContext;
                }

                public void Dispose()
                {
                    ParticipantRegistration? registration = Interlocked.Exchange(ref _registration, null);
                    registration?.ExitBind(_bindingId, _inheritedBindingContext);
                }
            }
        }
    }
}
