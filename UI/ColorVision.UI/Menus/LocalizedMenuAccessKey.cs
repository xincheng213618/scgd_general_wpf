using System.Globalization;

namespace ColorVision.UI.Menus;

/// <summary>
/// Formats WPF menu access keys according to the current UI language.
/// </summary>
public static class LocalizedMenuAccessKey
{
    public static string Format(string label, char accessKey, bool requiresInput = false)
    {
        ArgumentNullException.ThrowIfNull(label);
        char normalizedAccessKey = char.ToUpperInvariant(accessKey);

        if (string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "en", StringComparison.OrdinalIgnoreCase))
        {
            if (requiresInput)
                label += "...";
            string accessKeyText = normalizedAccessKey.ToString();
            int index = label.IndexOf(accessKeyText, StringComparison.OrdinalIgnoreCase);
            for (int candidate = index; candidate >= 0; candidate = label.IndexOf(accessKeyText, candidate + 1, StringComparison.OrdinalIgnoreCase))
            {
                if (candidate == 0 || !char.IsLetterOrDigit(label[candidate - 1]))
                {
                    index = candidate;
                    break;
                }
            }
            if (index >= 0)
                return label.Insert(index, "_");
        }

        return $"{label}(_{normalizedAccessKey})";
    }
}
