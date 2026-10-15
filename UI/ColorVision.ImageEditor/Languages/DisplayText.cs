using System;
using System.Globalization;
using System.Windows.Markup;

namespace ColorVision.ImageEditor;

internal static class DisplayText
{
    internal static string Get(string key) => Properties.Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    internal static string Format(FormattableString value, IFormatProvider? formatProvider = null) => string.Format(formatProvider ?? CultureInfo.CurrentCulture, Get(value.Format), value.GetArguments());
}

[MarkupExtensionReturnType(typeof(string))]
public sealed class DisplayTextExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) => DisplayText.Get(Key);
}
