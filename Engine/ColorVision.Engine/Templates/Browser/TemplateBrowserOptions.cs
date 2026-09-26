using System;

namespace ColorVision.Engine.Templates.Browser;

internal sealed record TemplateBrowserOptions(
    string Label,
    bool HasCovers,
    Func<string> GetOrderScope,
    Func<TemplateBase, string> GetOrderKey,
    Action<TemplateBase> SaveName);
