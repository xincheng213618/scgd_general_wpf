#pragma warning disable CA1513,CA1716,CA1863
using ColorVision.UI.Properties;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.UI
{
    internal static class TimedButtonOperationDefaults
    {
        private const double DefaultMinimumExpectedDurationMs = 500;

        public static string BuildOperationKey(FrameworkElement owner, string actionKey)
        {
            string ownerKey = ToKebabCase(owner.GetType().FullName ?? owner.GetType().Name);
            return $"{ownerKey}:{NormalizeActionKey(actionKey)}";
        }

        public static string BuildActionKey(Button button)
        {
            string? actionKey = NormalizeText(button.Name)
                ?? ExtractText(button.Content)
                ?? ExtractText(button.ToolTip);

            if (string.IsNullOrWhiteSpace(actionKey))
            {
                throw new InvalidOperationException("Timed button operation needs a named button or an explicit action key.");
            }

            return NormalizeActionKey(actionKey);
        }

        public static void ApplyButtonDefaults(Button button, TimedButtonOperationOptions options)
        {
            object? originalContent = button.Content;
            string buttonLabel = ResolveButtonLabel(button, originalContent);
            string tooltipLabel = ExtractText(button.ToolTip) ?? buttonLabel;

            if (string.IsNullOrWhiteSpace(options.OperationKey))
            {
                options.OperationKey = BuildOperationKey(button, BuildActionKey(button));
            }

            options.RunningText ??= buttonLabel;
            options.ProgressForeground ??= Brushes.Red;
            options.ContentFactory ??= _ => originalContent ?? buttonLabel;
            options.ToolTipFactory ??= stats => TimedButtonOperationTextFormatter.BuildTooltip(tooltipLabel, stats);

            if (options.MinimumExpectedDurationMs <= 0)
            {
                options.MinimumExpectedDurationMs = DefaultMinimumExpectedDurationMs;
            }
        }

        private static string ResolveButtonLabel(Button button, object? originalContent)
        {
            return ExtractText(originalContent)
                ?? ExtractText(button.ToolTip)
                ?? NormalizeText(button.Name)
                ?? "Operation";
        }

        private static string NormalizeActionKey(string value)
        {
            string text = NormalizeText(value) ?? string.Empty;
            text = RemoveSuffix(text, "Button");
            text = RemoveSuffix(text, "Btn");
            return ToKebabCase(text);
        }

        private static string RemoveSuffix(string text, string suffix)
        {
            return text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                ? text[..^suffix.Length]
                : text;
        }

        private static string ToKebabCase(string value)
        {
            string text = NormalizeText(value) ?? string.Empty;
            var builder = new StringBuilder(text.Length + 8);
            bool pendingSeparator = false;

            for (int i = 0; i < text.Length; i++)
            {
                char current = text[i];
                if (!char.IsLetterOrDigit(current))
                {
                    pendingSeparator = builder.Length > 0;
                    continue;
                }

                char previous = i > 0 ? text[i - 1] : '\0';
                char next = i + 1 < text.Length ? text[i + 1] : '\0';
                bool wordBoundary = i > 0
                    && char.IsUpper(current)
                    && (char.IsLower(previous) || char.IsDigit(previous) || (char.IsUpper(previous) && char.IsLower(next)));

                if ((pendingSeparator || wordBoundary) && builder.Length > 0 && builder[^1] != '-')
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(current));
                pendingSeparator = false;
            }

            return builder.Length == 0 ? "operation" : builder.ToString();
        }

        private static string? ExtractText(object? value)
        {
            switch (value)
            {
                case null:
                    return null;
                case string text:
                    return NormalizeText(text);
                case TextBlock textBlock:
                    return NormalizeText(textBlock.Text);
                case AccessText accessText:
                    return NormalizeText(accessText.Text);
                case ContentControl contentControl when !ReferenceEquals(contentControl.Content, value):
                    return ExtractText(contentControl.Content);
                case HeaderedContentControl headeredContentControl:
                    return ExtractText(headeredContentControl.Header);
                case HeaderedItemsControl headeredItemsControl:
                    return ExtractText(headeredItemsControl.Header);
                case DependencyObject dependencyObject:
                    foreach (object child in LogicalTreeHelper.GetChildren(dependencyObject))
                    {
                        string? childText = ExtractText(child);
                        if (!string.IsNullOrWhiteSpace(childText))
                        {
                            return childText;
                        }
                    }
                    return null;
                default:
                    return NormalizeText(value.ToString());
            }
        }

        private static string? NormalizeText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }

    public static class TimedButtonOperationTextFormatter
    {
        public static object BuildCompactContent(string label, TimedButtonOperationStats? stats)
        {
            return label;
        }

        public static string BuildTooltip(string label, TimedButtonOperationStats? stats)
        {
            if (stats == null || (stats.SuccessCount == 0 && stats.WarmupCount == 0))
            {
                return $"{label}\n{Resources.TimedOp_NoHistory}";
            }

            if (stats.SuccessCount == 0)
            {
                return $"{label}\n{Resources.TimedOp_Warmup} {FormatDuration(stats.WarmupElapsedMs)}";
            }

            return $"{label}\n{Resources.TimedOp_Last} {FormatDuration(stats.LastElapsedMs)} · {Resources.TimedOp_Average} {FormatDuration(stats.AverageElapsedMs)}";
        }

        public static string FormatDuration(double elapsedMilliseconds)
        {
            if (elapsedMilliseconds < 1000)
            {
                return $"{elapsedMilliseconds:F0} ms";
            }

            return $"{elapsedMilliseconds / 1000:F1} s";
        }

    }
}
