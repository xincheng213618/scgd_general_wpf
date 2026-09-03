using ColorVision.Database;
using SqlSugar;

namespace ColorVision.UI.Tests;

public sealed class LegacyDatabaseBrowserRegistrationTests
{
    [Fact]
    public void ShippedPluginRegistrationSignaturesBindWithoutAccessingDatabaseFactories()
    {
        var assembly = typeof(MySqlControl).Assembly;
        var providerType = assembly.GetType("ColorVision.Database.SqliteDatabaseBrowserProvider", throwOnError: true)!;
        var contractType = assembly.GetType("ColorVision.Database.IDatabaseBrowserProvider", throwOnError: true)!;
        var registryType = assembly.GetType("ColorVision.Database.DatabaseBrowserProviderRegistry", throwOnError: true)!;
        var constructor = providerType.GetConstructor([typeof(string), typeof(string), typeof(Func<string>), typeof(Func<string, SqlSugarClient>)]);
        var register = registryType.GetMethod("Register", [contractType]);

        Assert.NotNull(constructor);
        Assert.NotNull(register);
        Assert.True(register.IsStatic);
        Assert.Equal(typeof(void), register.ReturnType);
        Assert.True(contractType.IsAssignableFrom(providerType));

        Func<string> pathFactory = () => throw new InvalidOperationException("Retired registration must not resolve database paths.");
        Func<string, SqlSugarClient> clientFactory = _ => throw new InvalidOperationException("Retired registration must not open databases.");
        var provider = constructor.Invoke(["sqlite.legacy-plugin", "Legacy results", pathFactory, clientFactory]);

        Assert.Null(register.Invoke(null, [provider]));
    }
}
