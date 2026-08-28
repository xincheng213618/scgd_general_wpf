using ColorVision.Algorithms;
using ColorVision.UI;
using System;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    /// <summary>Captures one initiating WPF owner and never re-resolves it after an await.</summary>
    internal sealed class AlgorithmAnalysisWindowOwner
    {
        private readonly Window? _owner;

        private AlgorithmAnalysisWindowOwner(Window? owner) => _owner = owner;

        public static AlgorithmAnalysisWindowOwner Capture()
            => new(Application.Current?.GetActiveWindow());

        internal static AlgorithmAnalysisWindowOwner From(Window? owner) => new(owner);

        public Window? Current => IsAvailable ? _owner : null;

        public bool IsAvailable => _owner == null
            || (!_owner.Dispatcher.HasShutdownStarted
                && !_owner.Dispatcher.HasShutdownFinished
                && _owner.IsLoaded
                && _owner.IsVisible);

        public bool TryAssign(Window child)
        {
            ArgumentNullException.ThrowIfNull(child);
            if (!IsAvailable) return false;
            if (_owner != null) child.Owner = _owner;
            return true;
        }
    }

    /// <summary>Uses an owned message box only while the captured owner remains valid.</summary>
    internal static class AlgorithmAnalysisMessageBox
    {
        public static MessageBoxResult Show(
            AlgorithmAnalysisWindowOwner owner,
            string message,
            string caption,
            MessageBoxButton button,
            MessageBoxImage icon)
        {
            ArgumentNullException.ThrowIfNull(owner);
            Window? current = owner.Current;
            return current == null
                ? MessageBox.Show(message, caption, button, icon)
                : MessageBox.Show(current, message, caption, button, icon);
        }
    }

    /// <summary>Moves result ownership to a modeless window as one exception-safe transaction.</summary>
    internal static class AlgorithmAnalysisResultWindowTransaction
    {
        public static bool TryShow(
            AlgorithmResult result,
            AlgorithmAnalysisWindowOwner owner,
            Func<AlgorithmResult, Window> createWindow,
            Func<Window, bool> registerWindow,
            Action releaseSession,
            Window? previousWindow,
            out Exception? failure,
            Action<Window>? showWindow = null)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(createWindow);
            ArgumentNullException.ThrowIfNull(registerWindow);
            ArgumentNullException.ThrowIfNull(releaseSession);
            failure = null;
            Window? window = null;
            bool succeeded = false;
            try
            {
                if (!owner.IsAvailable) return false;
                DisposeWindow(previousWindow);
                window = createWindow(result);
                if (!owner.TryAssign(window)) return false;
                if (!registerWindow(window)) return false;
                (showWindow ?? (static value => value.Show()))(window);
                succeeded = true;
                return true;
            }
            catch (Exception exception)
            {
                failure = exception;
                return false;
            }
            finally
            {
                if (!succeeded)
                {
                    CaptureCleanupFailure(releaseSession, ref failure);
                    CaptureCleanupFailure(() => DisposeWindow(window), ref failure);
                    CaptureCleanupFailure(result.Dispose, ref failure);
                }
            }
        }

        private static void DisposeWindow(Window? window)
        {
            if (window is IDisposable disposable)
            {
                disposable.Dispose();
                return;
            }
            if (window == null) return;
            window.Close();
        }

        internal static void CaptureCleanupFailure(Action cleanup, ref Exception? failure)
        {
            try { cleanup(); }
            catch (Exception exception) { failure ??= exception; }
        }
    }
}
