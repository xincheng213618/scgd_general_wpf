using System;
using System.Globalization;
using System.Resources;
using System.Windows.Markup;

namespace ProjectKB;

internal static class DisplayText
{
    private static readonly ResourceManager Resources = new("ProjectKB.Properties.Resources", typeof(DisplayText).Assembly);

    internal static string Get(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    internal static string Format(FormattableString value) => string.Format(CultureInfo.CurrentCulture, Get(value.Format), value.GetArguments());
}

[MarkupExtensionReturnType(typeof(string))]
public sealed class DisplayTextExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) => DisplayText.Get(Key);
}
