using SpectrumResources = Spectrum.Properties.Resources;
using System.Globalization;
using System.Windows.Markup;

namespace Spectrum;

[MarkupExtensionReturnType(typeof(string))]
public sealed class ResxExtension : MarkupExtension
{
    public ResxExtension()
    {
    }

    public ResxExtension(string key)
    {
        Key = key;
    }

    [ConstructorArgument("key")]
    public string? Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrWhiteSpace(Key))
        {
            return string.Empty;
        }

        return SpectrumResources.ResourceManager.GetString(Key, SpectrumResources.Culture ?? CultureInfo.CurrentUICulture) ?? Key;
    }
}