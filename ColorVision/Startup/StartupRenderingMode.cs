using System;
using ColorVision.UI.Shell;
using System.Windows.Interop;

namespace ColorVision;

internal static class StartupRenderingMode
{
    internal const string ArgumentName = "software-rendering";

    internal static RenderMode? Resolve(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var parser = new ArgumentParser();
        parser.AddArgument(ArgumentName, true);
        ArgumentParseResult result = parser.ParseSnapshot(args);
        return result.Values.ContainsKey(ArgumentName) ? RenderMode.SoftwareOnly : null;
    }
}
