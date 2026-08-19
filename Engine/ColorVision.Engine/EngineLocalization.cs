#pragma warning disable CA2255
using ST.Library.UI;
using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Markup;

namespace ColorVision.Engine
{
    internal static class EngineLocalization
    {
        [ModuleInitializer]
        internal static void RegisterResources()
        {
            Lang.RegisterResourceManager(Properties.Resources.ResourceManager);
        }

        internal static string Get(string key) => Lang.Get(key);

        internal static string Format(FormattableString value)
        {
            string format = Lang.Get(value.Format);
            return string.Format(CultureInfo.CurrentCulture, format, value.GetArguments());
        }
    }

    [MarkupExtensionReturnType(typeof(string))]
    public sealed class EngineLangExtension : MarkupExtension
    {
        public string Key { get; set; } = string.Empty;

        public EngineLangExtension()
        {
        }

        public EngineLangExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider) => EngineLocalization.Get(Key);
    }
}
