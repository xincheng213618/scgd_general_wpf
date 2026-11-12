#pragma warning disable CA1513,CA1716,CA1863
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI
{
    public sealed class TimedButtonOperationRegistry : IDisposable
    {
        private readonly FrameworkElement _owner;
        private readonly Func<string, string> _operationKeyBuilder;
        private readonly Dictionary<Button, TimedButtonOperation> _operations = new Dictionary<Button, TimedButtonOperation>();
        private bool _isDisposed;

        internal TimedButtonOperationRegistry(FrameworkElement owner, Func<string, string> operationKeyBuilder)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(operationKeyBuilder);

            _owner = owner;
            _operationKeyBuilder = operationKeyBuilder;
            _owner.Unloaded += Owner_Unloaded;
        }

        internal bool IsDisposed => _isDisposed;

        public TimedButtonOperation Register(Button button, Action<TimedButtonOperationOptions>? configure = null)
        {
            return Register(button, TimedButtonOperationDefaults.BuildActionKey(button), configure);
        }

        public TimedButtonOperation Register(Button button, string actionKey, Action<TimedButtonOperationOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(button);
            ThrowIfDisposed();

            if (_operations.TryGetValue(button, out TimedButtonOperation? existingOperation))
            {
                return existingOperation;
            }

            TimedButtonOperationOptions options = new TimedButtonOperationOptions
            {
                OperationKey = _operationKeyBuilder(actionKey)
            };
            if (string.IsNullOrWhiteSpace(options.OperationKey))
            {
                options.OperationKey = _operationKeyBuilder(TimedButtonOperationDefaults.BuildActionKey(button));
            }

            TimedButtonOperationDefaults.ApplyButtonDefaults(button, options);
            configure?.Invoke(options);
            TimedButtonOperationDefaults.ApplyButtonDefaults(button, options);
            return Register(button, options);
        }

        public TimedButtonOperation Register(Button button, TimedButtonOperationOptions options)
        {
            ArgumentNullException.ThrowIfNull(button);
            ArgumentNullException.ThrowIfNull(options);
            ThrowIfDisposed();

            if (_operations.TryGetValue(button, out TimedButtonOperation? existingOperation))
            {
                return existingOperation;
            }

            if (string.IsNullOrWhiteSpace(options.OperationKey))
            {
                options.OperationKey = _operationKeyBuilder(TimedButtonOperationDefaults.BuildActionKey(button));
            }

            TimedButtonOperationDefaults.ApplyButtonDefaults(button, options);
            TimedButtonOperation operation = new TimedButtonOperation(button, options);
            _operations.Add(button, operation);
            return operation;
        }

        public bool Contains(Button button)
        {
            ArgumentNullException.ThrowIfNull(button);
            return !_isDisposed && _operations.ContainsKey(button);
        }

        public TimedButtonOperation? Get(Button button)
        {
            ArgumentNullException.ThrowIfNull(button);
            if (_isDisposed)
            {
                return null;
            }

            _operations.TryGetValue(button, out TimedButtonOperation? operation);
            return operation;
        }

        public TimedButtonOperationScope? Begin(Button button, double? expectedDurationMs = null, string? runningText = null)
        {
            ArgumentNullException.ThrowIfNull(button);
            TimedButtonOperation operation = Get(button) ?? Register(button);
            return operation.Begin(expectedDurationMs, runningText);
        }

        public void RefreshIdleState(Button? button = null)
        {
            if (_isDisposed)
            {
                return;
            }

            if (button != null)
            {
                Get(button)?.RefreshIdleState();
                return;
            }

            foreach (TimedButtonOperation operation in _operations.Values)
            {
                operation.RefreshIdleState();
            }
        }

        private void Owner_Unloaded(object sender, RoutedEventArgs e)
        {
            Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(TimedButtonOperationRegistry));
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _owner.Unloaded -= Owner_Unloaded;

            foreach (TimedButtonOperation operation in _operations.Values)
            {
                operation.Dispose();
            }

            _operations.Clear();
            GC.SuppressFinalize(this);
        }
    }

    public static class TimedButtonOperationRegistryExtensions
    {
        private static readonly ConditionalWeakTable<FrameworkElement, TimedButtonOperationRegistry> Registries = new ConditionalWeakTable<FrameworkElement, TimedButtonOperationRegistry>();

        public static TimedButtonOperationRegistry? TryGetTimedButtonOperations(this FrameworkElement owner)
        {
            ArgumentNullException.ThrowIfNull(owner);

            if (!Registries.TryGetValue(owner, out TimedButtonOperationRegistry? existingRegistry))
            {
                return null;
            }

            if (!existingRegistry.IsDisposed)
            {
                return existingRegistry;
            }

            Registries.Remove(owner);
            return null;
        }

        public static TimedButtonOperationRegistry GetTimedButtonOperations(this FrameworkElement owner, Func<string, string>? operationKeyBuilder = null)
        {
            ArgumentNullException.ThrowIfNull(owner);

            TimedButtonOperationRegistry? existingRegistry = owner.TryGetTimedButtonOperations();
            if (existingRegistry != null)
            {
                return existingRegistry;
            }

            TimedButtonOperationRegistry registry = new TimedButtonOperationRegistry(
                owner,
                operationKeyBuilder ?? (actionKey => TimedButtonOperationDefaults.BuildOperationKey(owner, actionKey)));
            Registries.Add(owner, registry);
            return registry;
        }

        public static void DisposeTimedButtonOperations(this FrameworkElement owner)
        {
            ArgumentNullException.ThrowIfNull(owner);

            TimedButtonOperationRegistry? registry = owner.TryGetTimedButtonOperations();
            if (registry == null)
            {
                return;
            }

            registry.Dispose();
            Registries.Remove(owner);
        }
    }
}
