using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot
{
    public sealed class CopilotAgentSessionCheckpoint
    {
        public const int MaxSerializedSessionCharacters = 4_000_000;

        public string ProfileKey { get; init; } = string.Empty;

        public string SerializedSessionJson { get; init; } = string.Empty;

        public DateTimeOffset UpdatedAtUtc { get; init; }

        public bool IsUsableFor(CopilotProfileConfig profile)
        {
            if (profile == null || !IsStructurallyValid() || !string.Equals(ProfileKey, CreateProfileKey(profile), StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }

        public bool IsStructurallyValid()
        {
            if (string.IsNullOrWhiteSpace(ProfileKey)
                || string.IsNullOrWhiteSpace(SerializedSessionJson)
                || SerializedSessionJson.Length > MaxSerializedSessionCharacters)
            {
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(SerializedSessionJson);
                return document.RootElement.ValueKind == JsonValueKind.Object;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public static CopilotAgentSessionCheckpoint? Create(CopilotProfileConfig profile, string serializedSessionJson)
        {
            ArgumentNullException.ThrowIfNull(profile);
            var json = serializedSessionJson?.Trim() ?? string.Empty;
            if (json.Length == 0 || json.Length > MaxSerializedSessionCharacters)
                return null;

            var checkpoint = new CopilotAgentSessionCheckpoint
            {
                ProfileKey = CreateProfileKey(profile),
                SerializedSessionJson = json,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            };
            return checkpoint.IsStructurallyValid() ? checkpoint : null;
        }

        public static string CreateProfileKey(CopilotProfileConfig profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            var value = string.Join("|", new[]
            {
                profile.Id?.Trim() ?? string.Empty,
                profile.ProviderType.ToString(),
                profile.BaseUrl?.Trim().TrimEnd('/') ?? string.Empty,
                profile.Model?.Trim() ?? string.Empty,
                profile.EffectiveSystemPrompt,
            });
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(hash.AsSpan(0, 16)).ToLowerInvariant();
        }
    }
}
