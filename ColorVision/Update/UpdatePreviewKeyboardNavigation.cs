using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ColorVision.Update;

internal sealed class UpdatePreviewKeyboardNavigation
{
    private readonly Window _window;
    private readonly UpdatePreviewDialogContext _context;
    private readonly Button _confirmButton;
    private readonly Button _cancelButton;
    private bool _closed;

    private UpdatePreviewKeyboardNavigation(Window window, UpdatePreviewDialogContext context, Button confirmButton, Button cancelButton)
    {
        _window = window;
        _context = context;
        _confirmButton = confirmButton;
        _cancelButton = cancelButton;
        window.ContentRendered += OnContentRendered;
        window.Closed += OnClosed;
        context.PropertyChanged += OnContextChanged;
    }

    internal static void Attach(Window window, UpdatePreviewDialogContext context, Button confirmButton, Button cancelButton)
        => _ = new UpdatePreviewKeyboardNavigation(window, context, confirmButton, cancelButton);

    private void OnContentRendered(object? sender, EventArgs e)
    {
        _window.ContentRendered -= OnContentRendered;
        QueueFocus(initial: true);
    }

    private void OnContextChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UpdatePreviewDialogContext.IsChecking) && !_context.IsChecking)
            QueueFocus();
    }

    private void QueueFocus(bool initial = false)
    {
        // CopyFrom publishes checking state before replacing the items. Wait for the final layout.
        _window.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (_closed || !_window.IsActive)
                return;

            IInputElement? focused = Keyboard.FocusedElement;
            if (!initial && focused != null && focused != _window && focused != _cancelButton && _window.IsKeyboardFocusWithin)
                return;

            Button target = _context.CanConfirm ? _confirmButton : _cancelButton;
            if (target.IsVisible && target.IsEnabled)
                target.Focus();
        }));
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _closed = true;
        _context.PropertyChanged -= OnContextChanged;
        _window.ContentRendered -= OnContentRendered;
        _window.Closed -= OnClosed;
    }
}
