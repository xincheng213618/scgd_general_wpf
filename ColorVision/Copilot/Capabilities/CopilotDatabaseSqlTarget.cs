using MySqlConnector;
using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed record CopilotDatabaseSqlTarget(string Fingerprint, string DisplayText)
    {
        internal static CopilotDatabaseSqlTarget FromConnectionString(string connectionString)
        {
            MySqlConnectionStringBuilder builder;
            try
            {
                builder = new MySqlConnectionStringBuilder(connectionString);
            }
            catch (ArgumentException)
            {
                // Parser diagnostics can include a malformed credential fragment.
                throw new InvalidOperationException("The configured database target could not be read. Check the connection settings before requesting approval.");
            }
            var identity = new[] { builder.Server.Trim().ToLowerInvariant(), builder.Port.ToString(CultureInfo.InvariantCulture), builder.Database, builder.UserID };
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(identity)))).ToLowerInvariant();
            return new(fingerprint, $"server: {builder.Server}\nport: {builder.Port}\ndatabase: {builder.Database}\naccount: {builder.UserID}");
        }

        internal static void EnsureMatches(string connectionString, string expectedFingerprint)
        {
            if (!string.IsNullOrEmpty(expectedFingerprint)
                && !string.Equals(FromConnectionString(connectionString).Fingerprint, expectedFingerprint, StringComparison.Ordinal))
                throw new CopilotDatabaseTargetChangedException();
        }
    }

    internal sealed class CopilotDatabaseTargetChangedException : Exception
    {
        public CopilotDatabaseTargetChangedException()
            : base("The configured database target changed after approval. Review the current target and request a new approval.")
        {
        }
    }

    // Keep the public executor API compatible; target-aware native writes use this binding.
    internal interface ICopilotBoundDatabaseSqlExecutor : ICopilotDatabaseSqlExecutor
    {
        CopilotDatabaseSqlTarget CaptureTarget();

        Task<CopilotDatabaseMutationResult> ExecuteBoundAsync(string sql, CopilotDatabaseSqlAnalysis analysis, int timeoutSeconds, string expectedTargetFingerprint, CancellationToken cancellationToken);
    }
}
