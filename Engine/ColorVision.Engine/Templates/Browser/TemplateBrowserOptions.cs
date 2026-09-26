using System;

namespace ColorVision.Engine.Templates.Browser;

internal sealed record TemplateBrowserOptions(
    string Label,
    bool HasCovers,
    Action<TemplateBase> SaveName);
