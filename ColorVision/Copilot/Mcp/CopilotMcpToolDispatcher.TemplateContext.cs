using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Windows;

namespace ColorVision.Copilot.Mcp
{
    internal sealed partial class CopilotMcpToolDispatcher
    {
        private CopilotMcpToolCallResult GetActiveTemplateContext()
        {
            var liveContext = _environment.LiveContextProvider();
            if (liveContext == null)
                return CopilotMcpToolCallResult.Ok("No active template context is currently published.");

            if (!liveContext.SourceId.StartsWith("template-json-editor:", StringComparison.OrdinalIgnoreCase))
                return CopilotMcpToolCallResult.Ok("The current live context is not a template editor context.");

            return CopilotMcpToolCallResult.Ok(FormatTemplateLiveContext(liveContext));
        }

        private static CopilotMcpToolCallResult GetSavedTemplateContext(IReadOnlyDictionary<string, JsonElement>? arguments)
        {
            var templateCode = GetString(arguments, "template_code", "code");
            var templateName = GetString(arguments, "template_name", "name");
            if (string.IsNullOrWhiteSpace(templateCode) || string.IsNullOrWhiteSpace(templateName))
                return CopilotSavedTemplateContextSupport.Read(templateCode, templateName);

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                return dispatcher.Invoke(() =>
                    CopilotSavedTemplateContextSupport.Read(templateCode, templateName));
            }

            return CopilotSavedTemplateContextSupport.Read(templateCode, templateName);
        }

        private static CopilotMcpToolCallResult GetTemplateTypeContext(IReadOnlyDictionary<string, JsonElement>? arguments)
        {
            var templateCode = GetString(arguments, "template_code", "code");
            if (string.IsNullOrWhiteSpace(templateCode))
                return CopilotSavedTemplateContextSupport.ReadType(templateCode);

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
                return dispatcher.Invoke(() => CopilotSavedTemplateContextSupport.ReadType(templateCode));

            return CopilotSavedTemplateContextSupport.ReadType(templateCode);
        }
    }
}
