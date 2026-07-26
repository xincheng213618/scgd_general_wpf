using System;
using System.Globalization;
using System.Text;

namespace ColorVision.Copilot
{
    internal static class CopilotApprovalReviewTextEncoder
    {
        public static string Encode(string? value)
        {
            var builder = new StringBuilder(value?.Length ?? 0);
            Append(builder, value);
            return builder.ToString();
        }

        public static void Append(StringBuilder builder, string? value)
        {
            ArgumentNullException.ThrowIfNull(builder);
            value ??= string.Empty;
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                var category = char.GetUnicodeCategory(character);
                if (character == '\\')
                {
                    builder.Append(@"\\");
                }
                else if (character == '\r')
                {
                    builder.Append(@"\r");
                    if (index + 1 >= value.Length || value[index + 1] != '\n')
                        builder.AppendLine();
                }
                else if (character == '\n')
                {
                    builder.Append(@"\n").AppendLine();
                }
                else if (character == '\t')
                {
                    builder.Append(@"\t");
                }
                else if (category is UnicodeCategory.Control
                    or UnicodeCategory.Format
                    or UnicodeCategory.LineSeparator
                    or UnicodeCategory.ParagraphSeparator
                    || category == UnicodeCategory.SpaceSeparator && character != ' ')
                {
                    builder.Append(@"\u")
                        .Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
                }
                else
                {
                    builder.Append(character);
                }
            }
        }
    }
}
