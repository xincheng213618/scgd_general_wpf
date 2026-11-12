using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    // Used only on the parameter window's Dispatcher. Draft text can change before a
    // LostFocus binding updates its slider, so it has a separate confirmation revision.
    internal sealed class AlgorithmParameterInputs : IDisposable
    {
        private readonly List<Input> _inputs = new();
        private static readonly ExceptionValidationRule SourceErrorRule = new();
        private readonly Action? _changed;
        private bool _disposed;

        public AlgorithmParameterInputs(FrameworkElement scope, Action? changed = null)
        {
            ArgumentNullException.ThrowIfNull(scope);
            _changed = changed;
            foreach (TextBox box in FindInputs(scope, scope))
            {
                _inputs.Add(new Input(box));
                box.TextChanged += TextChanged;
            }
        }

        public long Revision { get; private set; }

        public bool TryCommit()
        {
            if (_disposed) return false;
            bool valid = true;
            foreach (Input input in _inputs)
            {
                BindingExpression? binding = input.Box.GetBindingExpression(TextBox.TextProperty);
                if (input.RejectedBinding != null && !ReferenceEquals(input.RejectedBinding, binding))
                {
                    ClearFeedback(input);
                    if (_disposed) return false;
                }
                if (binding == null)
                {
                    valid = false;
                    continue;
                }
                // UpdateSource can erase a manually marked error even though the same
                // rejected draft is still invalid and IsDirty is already false.
                if (ReferenceEquals(input.RejectedBinding, binding)
                    && string.Equals(input.RejectedText, input.Box.Text, StringComparison.Ordinal))
                {
                    ShowFailure(input, binding);
                    if (_disposed) return false;
                    valid = false;
                    continue;
                }
                binding.UpdateSource();
                if (_disposed) return false;
                // RangeBase rejects values such as NaN without necessarily setting the
                // validation-error flags. Do not clear that failure with UpdateTarget.
                if (IsValid(binding, input.Box))
                {
                    binding.UpdateTarget();
                    if (_disposed) return false;
                }
                if (!IsValid(binding, input.Box))
                {
                    if (binding.Status == BindingStatus.UpdateSourceError)
                    {
                        input.RejectedBinding = binding;
                        input.RejectedText = input.Box.Text;
                    }
                    ShowFailure(input, binding);
                    if (_disposed) return false;
                    valid = false;
                }
            }
            return valid;
        }

        private void TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_disposed) return;
            foreach (Input input in _inputs)
            {
                if (!ReferenceEquals(sender, input.Box) || string.Equals(input.Text, input.Box.Text, StringComparison.Ordinal)) continue;
                input.Text = input.Box.Text;
                Revision++;
                ClearFeedback(input);
                if (_disposed) return;
                _changed?.Invoke();
                return;
            }
        }

        private static bool IsValid(BindingExpression binding, TextBox box)
            => binding.Status == BindingStatus.Active && !binding.HasError && !Validation.GetHasError(box);

        private void ShowFailure(Input input, BindingExpression binding)
        {
            if (!binding.HasError && !Validation.GetHasError(input.Box)
                && (binding.Status == BindingStatus.UpdateSourceError || ReferenceEquals(input.RejectedBinding, binding)))
            {
                input.OwnedError = new ValidationError(SourceErrorRule, binding, DisplayText.Get("请修正参数中的错误输入。"), null);
                Validation.MarkInvalid(binding, input.OwnedError);
                if (_disposed) return;
            }

            object message = binding.ValidationError?.ErrorContent ?? DisplayText.Get("请修正参数中的错误输入。");
            if (input.OwnedToolTip != null && ReferenceEquals(input.Box.ToolTip, input.OwnedToolTip))
            {
                input.OwnedToolTip.Content = message;
            }
            else if (input.Box.ToolTip == null && input.Box.ReadLocalValue(FrameworkElement.ToolTipProperty) == DependencyProperty.UnsetValue)
            {
                input.OwnedToolTip = new ToolTip { Content = message };
                input.Box.SetValue(FrameworkElement.ToolTipProperty, input.OwnedToolTip);
            }
        }

        private static void ClearFeedback(Input input)
        {
            BindingExpression? binding = input.RejectedBinding;
            ValidationError? error = input.OwnedError;
            ToolTip? toolTip = input.OwnedToolTip;
            input.RejectedBinding = null;
            input.RejectedText = null;
            input.OwnedError = null;
            input.OwnedToolTip = null;
            if (binding != null && error != null && ReferenceEquals(binding.ValidationError, error)) Validation.ClearInvalid(binding);
            if (toolTip != null)
            {
                toolTip.IsOpen = false;
                if (ReferenceEquals(input.Box.ToolTip, toolTip)) input.Box.ClearValue(FrameworkElement.ToolTipProperty);
            }
        }

        private static IEnumerable<TextBox> FindInputs(DependencyObject node, FrameworkElement scope)
        {
            if (node is TextBox box)
            {
                BindingExpression? binding = box.GetBindingExpression(TextBox.TextProperty);
                if (binding?.ParentBinding.Path?.Path == nameof(RangeBase.Value)
                    && (binding.DataItem is RangeBase || (!string.IsNullOrEmpty(binding.ParentBinding.ElementName)
                        && scope.FindName(binding.ParentBinding.ElementName) is RangeBase)))
                    yield return box;
            }
            foreach (object child in LogicalTreeHelper.GetChildren(node))
                if (child is DependencyObject element)
                    foreach (TextBox input in FindInputs(element, scope)) yield return input;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (Input input in _inputs)
            {
                input.Box.TextChanged -= TextChanged;
                ClearFeedback(input);
            }
            _inputs.Clear();
        }

        private sealed class Input(TextBox box)
        {
            public TextBox Box { get; } = box;
            public string Text { get; set; } = box.Text;
            public BindingExpression? RejectedBinding { get; set; }
            public string? RejectedText { get; set; }
            public ValidationError? OwnedError { get; set; }
            public ToolTip? OwnedToolTip { get; set; }
        }
    }
}
