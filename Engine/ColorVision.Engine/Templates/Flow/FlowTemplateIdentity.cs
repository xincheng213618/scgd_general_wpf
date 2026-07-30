using System;

namespace ColorVision.Engine.Templates.Flow
{
    /// <summary>
    /// Creates a stable runtime identity without changing the serialized flow
    /// document. New flow resources already use a GUID code; legacy resources
    /// fall back to their resource row identity, which survives template order
    /// swaps.
    /// </summary>
    internal static class FlowTemplateIdentity
    {
        public static string? Create(
            int templateId,
            int? resourceId,
            string? resourceCode)
        {
            if (Guid.TryParse(resourceCode, out Guid code))
                return $"flow:{code:N}";

            if (resourceId is > 0)
                return $"flow-resource:{resourceId.Value}";

            return templateId > 0
                ? $"flow-template:{templateId}"
                : null;
        }
    }
}
