using ColorVision.Copilot;
using System;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotConfigWebPageNetworkTests
{
    [Theory]
    [InlineData("web")]
    [InlineData("nat64")]
    [InlineData("pref64")]
    public void SettingsCommandRoutesWebNetworkAliasesToTheWebTab(string argument)
    {
        Assert.True(CopilotSettingsCommand.TryResolvePage(argument, out var page));
        Assert.Equal(CopilotSettingsPage.Web, page);
        Assert.Equal(4, (int)page);
        Assert.Equal(2, (int)CopilotSettingsPage.Mcp);
        Assert.Equal(3, (int)CopilotSettingsPage.BackendSync);
        Assert.Equal(2, CopilotSettingsWindow.GetTabIndex(page));
        Assert.Equal(3, CopilotSettingsWindow.GetTabIndex(CopilotSettingsPage.Mcp));
        Assert.Equal(4, CopilotSettingsWindow.GetTabIndex(CopilotSettingsPage.BackendSync));

        var command = Assert.IsType<CopilotLocalCommand>(CopilotLocalCommandCatalog.FindExact("/settings"));
        Assert.Equal("/settings [models|agent|web|mcp|sync]", command.Usage);
        Assert.Contains(command.Arguments!, candidate => candidate.Value == "web");
    }

    [Fact]
    public void LegacyConfigurationMigratesWithNoConfiguredPref64Prefixes()
    {
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion - 1,
            WebPagePref64Prefixes = string.Empty,
        };

        Assert.True(config.EnsureInitialized());

        Assert.Equal(CopilotConfig.CurrentSchemaVersion, config.SchemaVersion);
        Assert.Empty(config.WebPagePref64Prefixes);
    }

    [Fact]
    public void EnsureInitializedCanonicalizesAndDeduplicatesValidPref64Prefixes()
    {
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion,
            WebPagePref64Prefixes = """
                # Network-specific Pref64 prefixes
                2001:db8:64::/96
                2001:0db8:0064:0000:0000:0000:0000:0000/96 # duplicate
                2001:db8:6500::/40
                """,
        };

        Assert.True(config.EnsureInitialized());

        Assert.Equal(
            $"2001:db8:64::/96{Environment.NewLine}2001:db8:6500::/40",
            config.WebPagePref64Prefixes);
    }

    [Fact]
    public void EnsureInitializedPreservesInvalidNonEmptyPref64Configuration()
    {
        const string invalid = "2001:db8:64::1/96";
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion - 1,
            WebPagePref64Prefixes = invalid,
        };

        Assert.True(config.EnsureInitialized());

        Assert.Equal(invalid, config.WebPagePref64Prefixes);
        Assert.Equal(CopilotConfig.CurrentSchemaVersion, config.SchemaVersion);
        Assert.False(CopilotWebPagePref64Configuration.TryParse(
            config.WebPagePref64Prefixes,
            out _,
            out var error));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void FutureConfigurationDoesNotNormalizePref64Prefixes()
    {
        const string futureText = "2001:0db8:0064::/96";
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion + 1,
            WebPagePref64Prefixes = futureText,
        };

        Assert.False(config.EnsureInitialized());

        Assert.Equal(futureText, config.WebPagePref64Prefixes);
    }

    [Theory]
    [InlineData("10.0.0.0/96")]
    [InlineData("::ffff:192.0.2.1/96")]
    [InlineData("fe80::%3/96")]
    [InlineData("2001:4860::%0/32")]
    [InlineData("[2001:4860::]/32")]
    [InlineData("2001:db8::/72")]
    [InlineData("2001:db8::1/96")]
    [InlineData("2001:db8:0:0:100::/96")]
    [InlineData("2001:db8::")]
    public void Pref64ParserRejectsUnsafeOrUnsupportedEntries(string value)
    {
        Assert.False(CopilotWebPagePref64Configuration.TryParse(value, out _, out var error));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void EffectiveConfigDiagnosticsReportsPref64MetadataWithoutLeakingPrefixes()
    {
        const string prefix = "2001:db8:64::/96";
        var directory = Path.Combine(Path.GetTempPath(), $"copilot-pref64-{Guid.NewGuid():N}");
        var configPath = Path.Combine(directory, "ColorVision.config.json");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(
                configPath,
                $$"""
                {
                  "CopilotConfig": {
                    "SchemaVersion": {{CopilotConfig.CurrentSchemaVersion}},
                    "WebPagePref64Prefixes": "{{prefix}}"
                  }
                }
                """);
            var report = CopilotEffectiveConfigDiagnostics.Format(new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig
                {
                    SchemaVersion = CopilotConfig.CurrentSchemaVersion,
                    WebPagePref64Prefixes = prefix,
                },
                State = new CopilotChatState(),
                ConfigFilePath = configPath,
            });

            Assert.Contains("公网 Web Pref64：RFC 7050 自动发现开启 · 配置 1 个", report, StringComparison.Ordinal);
            Assert.Contains("来源 应用配置 CopilotConfig", report, StringComparison.Ordinal);
            Assert.DoesNotContain(prefix, report, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void EffectiveConfigDiagnosticsReportsInvalidPref64AsFailClosedForAllWebFetches()
    {
        const string invalidPrefix = "2001:db8:64::1/96";
        var report = CopilotEffectiveConfigDiagnostics.Format(new CopilotEffectiveConfigDiagnosticContext
        {
            Config = new CopilotConfig
            {
                SchemaVersion = CopilotConfig.CurrentSchemaVersion,
                WebPagePref64Prefixes = invalidPrefix,
            },
            State = new CopilotChatState(),
        });

        Assert.Contains("公网 Web Pref64：配置语法无效（公网 Web 抓取失败关闭）", report, StringComparison.Ordinal);
        Assert.DoesNotContain("公网 Web Pref64：RFC 7050", report, StringComparison.Ordinal);
        Assert.DoesNotContain(invalidPrefix, report, StringComparison.Ordinal);
    }
}
