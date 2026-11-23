using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Solution.Themes;

/// <summary>Fits both ends of a document title into the space left by its tab commands.</summary>
public sealed class DocumentTabCaption : Decorator
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(DocumentTabCaption),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure));

    private readonly TextBlock label = new();

    public DocumentTabCaption()
    {
        Child = label;
        ClipToBounds = true;
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        string title = Title ?? string.Empty;
        Size fullSize = MeasureText(title);
        if (fullSize.Width <= availableSize.Width)
            return fullSize;

        // Measure the actual label so glyph fallback, inherited weight and display DPI
        // are the same during fitting and rendering. Only shorten on grapheme boundaries.
        int[] starts = StringInfo.ParseCombiningCharacters(title);
        int dot = title.LastIndexOf('.');
        int preferredSuffix = 0;
        if (dot > 0)
        {
            // Measurement files often end with a useful token such as "_F5.6W.cvraw".
            int separator = title.AsSpan(0, dot).LastIndexOfAny('_', ' ');
            if (separator > 0 && separator + 1 < dot)
                preferredSuffix = SuffixLengthIfItFits(separator + 1);
            if (preferredSuffix == 0)
                preferredSuffix = SuffixLengthIfItFits(dot);
        }
        int low = 0;
        int high = starts.Length - 1;
        string best = string.Empty;
        while (low <= high)
        {
            int kept = low + (high - low) / 2;
            int suffix = kept == 0 ? 0 : Math.Min(kept - 1, Math.Max(kept / 2, preferredSuffix));
            int prefixEnd = starts[kept - suffix];
            int suffixStart = suffix == 0 ? title.Length : starts[starts.Length - suffix];
            string candidate = string.Concat(title.AsSpan(0, prefixEnd), "…", title.AsSpan(suffixStart));
            if (MeasureText(candidate).Width <= availableSize.Width)
            {
                best = candidate;
                low = kept + 1;
            }
            else
            {
                high = kept - 1;
            }
        }

        MeasureText(best);
        return new Size(availableSize.Width, fullSize.Height);

        int SuffixLengthIfItFits(int start)
        {
            int element = Array.BinarySearch(starts, start);
            // For long dotted service names, leave at least a third for the prefix.
            return element >= 0 && MeasureText(title[start..]).Width <= availableSize.Width * 2 / 3
                ? starts.Length - element : 0;
        }
    }

    private Size MeasureText(string text)
    {
        label.Text = text;
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return label.DesiredSize;
    }
}
