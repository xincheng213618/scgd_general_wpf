using ColorVision.UI.Serach;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Resources;

namespace ColorVision.UI.Tests;

public class WpfResourceEmbeddingTests
{
    [Fact]
    public void SearchControl_CompiledXamlIsEmbedded()
    {
        Assert.True(ContainsCompiledXaml(typeof(SearchControl).Assembly, "serach/searchcontrol.baml"));
    }

    [Fact]
    public void SearchWindow_CompiledXamlIsEmbedded()
    {
        Assert.True(ContainsCompiledXaml(typeof(SearchWindow).Assembly, "serach/searchwindow.baml"));
    }

    private static bool ContainsCompiledXaml(Assembly assembly, string resourceKey)
    {
        foreach (string resourceName in assembly.GetManifestResourceNames().Where(name => name.EndsWith(".g.resources", StringComparison.Ordinal)))
        {
            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                continue;

            using var reader = new ResourceReader(stream);
            foreach (DictionaryEntry entry in reader)
            {
                if (string.Equals(entry.Key as string, resourceKey, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }
}
