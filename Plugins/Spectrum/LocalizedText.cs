using Spectrum.Properties;
using System.Globalization;

namespace Spectrum
{
    internal static class LocalizedText
    {
        internal static string Get(string key)
        {
            return Resources.ResourceManager.GetString(key, Resources.Culture ?? CultureInfo.CurrentUICulture) ?? key;
        }

        internal static string Format(string key, params object[] args)
        {
            return string.Format(Get(key), args);
        }
    }
}