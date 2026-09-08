using System;
using System.Linq;

namespace ColorVision.Copilot
{
    internal static class CopilotConfiguredModelSelection
    {
        public const int MaximumModelCharacters = 256;

        public static bool TryNormalize(string? value, out string model)
        {
            model = (value ?? string.Empty).Trim();
            return model.Length is > 0 and <= MaximumModelCharacters
                && !model.Any(char.IsControl);
        }
    }
}
