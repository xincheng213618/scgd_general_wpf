using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace ColorVision.UI.Serach;

public sealed class SearchHighlightTextBlock : TextBlock
{
    public static readonly DependencyProperty ContentTextProperty = DependencyProperty.Register(nameof(ContentText), typeof(string),
        typeof(SearchHighlightTextBlock), new PropertyMetadata(string.Empty, Refresh));
    public static readonly DependencyProperty QueryProperty = DependencyProperty.Register(nameof(Query), typeof(string),
        typeof(SearchHighlightTextBlock), new PropertyMetadata(string.Empty, Refresh));
    public string ContentText { get => (string)GetValue(ContentTextProperty); set => SetValue(ContentTextProperty, value); }
    public string Query { get => (string)GetValue(QueryProperty); set => SetValue(QueryProperty, value); }

    private static void Refresh(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var control = (SearchHighlightTextBlock)sender;
        control.Inlines.Clear();
        foreach ((string text, bool matched) in SplitHighlights(control.ContentText, control.Query))
        {
            var run = new Run(text);
            if (matched)
            {
                run.FontWeight = FontWeights.SemiBold;
                run.SetResourceReference(TextElement.ForegroundProperty, "PrimaryBrush");
            }
            control.Inlines.Add(run);
        }
    }

    internal static IEnumerable<(string Text, bool Matched)> SplitHighlights(string? text, string? query)
    {
        text ??= string.Empty;
        if (text.Length == 0) yield break;
        bool[] matches = new bool[text.Length];
        foreach (string word in (query ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            for (int offset = 0; offset < text.Length;)
            {
                int index = text.IndexOf(word, offset, StringComparison.OrdinalIgnoreCase);
                if (index < 0) break;
                Array.Fill(matches, true, index, word.Length);
                offset = index + word.Length;
            }
        }
        int start = 0;
        for (int index = 1; index <= text.Length; index++)
        {
            if (index < text.Length && matches[index] == matches[start]) continue;
            yield return (text[start..index], matches[start]);
            start = index;
        }
    }
}
