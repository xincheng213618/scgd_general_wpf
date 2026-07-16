using System;
using System.Text;

namespace ColorVision.Copilot
{
    public static class CopilotToolFailureCode
    {
        public const int MaxLength = 64;

        public static string Normalize(string? value)
        {
            var source = (value ?? string.Empty).Trim();
            if (source.Length == 0)
                return string.Empty;

            var builder = new StringBuilder(Math.Min(source.Length, MaxLength));
            foreach (var character in source)
            {
                if (builder.Length >= MaxLength)
                    break;

                if (character is >= 'A' and <= 'Z')
                {
                    builder.Append((char)(character + ('a' - 'A')));
                }
                else if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
                {
                    builder.Append(character);
                }
                else if (builder.Length > 0 && builder[^1] != '_')
                {
                    builder.Append('_');
                }
            }

            return builder.ToString().TrimEnd('_');
        }
    }
}
