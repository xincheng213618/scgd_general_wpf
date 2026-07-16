using System;

namespace ColorVision.Copilot
{
    internal static class CopilotTokenEstimator
    {
        public const int AsciiCharactersPerToken = 4;
        private const int NonAsciiCharacterWeight = AsciiCharactersPerToken;

        public static long EstimateTextWeight(string? value)
        {
            // ASCII-heavy prompts average roughly four characters per token. CJK and
            // other non-ASCII text are conservatively treated as one UTF-16 unit per token.
            long weight = 0;
            foreach (var character in value ?? string.Empty)
                weight += character <= 0x7f ? 1 : NonAsciiCharacterWeight;
            return weight;
        }

        public static int WeightToTokenEstimate(long weight)
        {
            var normalized = Math.Max(1, weight);
            var tokens = normalized / AsciiCharactersPerToken
                + (normalized % AsciiCharactersPerToken == 0 ? 0 : 1);
            return (int)Math.Clamp(tokens, 1, int.MaxValue);
        }

        public static int GetPrefixLengthWithinWeight(string? value, long maximumWeight)
        {
            if (string.IsNullOrEmpty(value) || maximumWeight <= 0)
                return 0;

            long weight = 0;
            var length = 0;
            while (length < value.Length)
            {
                var characterWeight = value[length] <= 0x7f ? 1 : NonAsciiCharacterWeight;
                if (weight + characterWeight > maximumWeight)
                    break;
                weight += characterWeight;
                length++;
            }

            if (length > 0
                && length < value.Length
                && char.IsHighSurrogate(value[length - 1])
                && char.IsLowSurrogate(value[length]))
            {
                length--;
            }
            return length;
        }
    }
}
