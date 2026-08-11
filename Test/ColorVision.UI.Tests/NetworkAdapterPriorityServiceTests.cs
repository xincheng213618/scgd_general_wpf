using ColorVision.ToolPlugins.ThirdPartyApps;

namespace ColorVision.UI.Tests
{
    public class NetworkAdapterPriorityServiceTests
    {
        [Fact]
        public void InternalAppProvider_RegistersNetworkAdapterPriorityTool()
        {
            var app = Assert.Single(new InternalAppProvider().GetThirdPartyApps());

            Assert.Equal("上网网卡选择", app.Name);
            Assert.NotNull(app.LaunchAction);
        }

        [Fact]
        public void ParseAdapters_PreservesChineseAliasAndSortsPreferredRouteFirst()
        {
            const string json = """
                [
                  {
                    "InterfaceIndex": 18,
                    "InterfaceAlias": "以太网 2",
                    "ConnectionState": "Disconnected",
                    "InterfaceMetric": 25,
                    "AutomaticMetric": "Enabled",
                    "IPv4Address": "10.0.0.2",
                    "DefaultGateway": "",
                    "RouteMetric": null
                  },
                  {
                    "InterfaceIndex": 7,
                    "InterfaceAlias": "以太网",
                    "ConnectionState": "Connected",
                    "InterfaceMetric": 5,
                    "AutomaticMetric": "Disabled",
                    "IPv4Address": "192.168.1.20",
                    "DefaultGateway": "192.168.1.1",
                    "RouteMetric": 0
                  }
                ]
                """;

            var adapters = NetworkAdapterPriorityService.ParseAdapters(json);

            Assert.Equal(2, adapters.Count);
            Assert.Equal("以太网", adapters[0].InterfaceAlias);
            Assert.Equal("5", adapters[0].EffectiveMetricText);
            Assert.Equal("以太网 2", adapters[1].InterfaceAlias);
        }

        [Fact]
        public void BuildSetPreferredScript_UsesInterfaceIndexAndConfirmedMetric()
        {
            string script = NetworkAdapterPriorityService.BuildSetPreferredScript(12);

            Assert.Contains("-InterfaceIndex 12", script, StringComparison.Ordinal);
            Assert.Contains("-AutomaticMetric Disabled", script, StringComparison.Ordinal);
            Assert.Contains("-InterfaceMetric 5", script, StringComparison.Ordinal);
            Assert.DoesNotContain("InterfaceAlias", script, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildRestoreAutomaticMetricScript_EnablesAutomaticMetric()
        {
            string script = NetworkAdapterPriorityService.BuildRestoreAutomaticMetricScript(12);

            Assert.Contains("-InterfaceIndex 12", script, StringComparison.Ordinal);
            Assert.Contains("-AutomaticMetric Enabled", script, StringComparison.Ordinal);
            Assert.DoesNotContain("-InterfaceMetric", script, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildSetPreferredScript_RejectsInvalidInterfaceIndex()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => NetworkAdapterPriorityService.BuildSetPreferredScript(0));
        }
    }
}
