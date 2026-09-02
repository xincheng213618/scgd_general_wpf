using SqlSugar;
using System;

#pragma warning disable CS0618 // Preserve the signatures referenced by already shipped plugin binaries.

namespace ColorVision.Database
{
    [Obsolete("The built-in database browser has been removed. Kept only for existing plugin binaries.")]
    public interface IDatabaseBrowserProvider
    {
    }

    [Obsolete("The built-in database browser has been removed. This compatibility type does not access databases.")]
    public sealed class SqliteDatabaseBrowserProvider : IDatabaseBrowserProvider
    {
        public SqliteDatabaseBrowserProvider(string providerId, string providerName, Func<string> dbPathFactory, Func<string, SqlSugarClient> clientFactory)
        {
            // Old result managers construct this type during startup. Do not retain or invoke their factories.
        }
    }

    [Obsolete("The built-in database browser has been removed. Registration is ignored for existing plugin binaries.")]
    public static class DatabaseBrowserProviderRegistry
    {
        public static void Register(IDatabaseBrowserProvider provider)
        {
            // Preserve startup of previously published ARVR, LUX and Spectrum plugins without exposing a tool.
        }
    }
}
