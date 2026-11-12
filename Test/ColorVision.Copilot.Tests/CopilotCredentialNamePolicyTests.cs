using ColorVision.Copilot.Mcp;
using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCredentialNamePolicyTests
{
    [Fact]
    public void FirstClassifierInitializationInTurkishCulturePreservesOrdinalCredentialNames()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var context = new AssemblyLoadContext("credential-policy-turkish-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var assembly = context.LoadFromAssemblyPath(typeof(CopilotBusinessContextBuilder).Assembly.Location);
            Assert.NotSame(typeof(CopilotBusinessContextBuilder).Assembly, assembly);
            Assert.Same(context, AssemblyLoadContext.GetLoadContext(assembly));
            var classifier = assembly.GetType(typeof(CopilotBusinessContextBuilder).FullName!, throwOnError: true)!;
            var method = classifier.GetMethod(nameof(CopilotBusinessContextBuilder.IsSensitivePropertyName), BindingFlags.Public | BindingFlags.Static)!;

            Assert.True(Assert.IsType<bool>(method.Invoke(null, new object[] { "APIKEY" })));
            Assert.True(Assert.IsType<bool>(method.Invoke(null, new object[] { "PRIVATE_KEY" })));
            var textRedactor = assembly.GetType(typeof(CopilotSensitiveTextRedactor).FullName!, throwOnError: true)!;
            var redact = textRedactor.GetMethod(nameof(CopilotSensitiveTextRedactor.Redact), BindingFlags.Public | BindingFlags.Static)!;
            Assert.Equal("AUTHORIZATION: <redacted>\r\nmode=Run",
                Assert.IsType<string>(redact.Invoke(null, new object[] { "AUTHORIZATION: Basic alpha\r\nmode=Run" })));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            context.Unload();
        }
    }

    [Theory]
    [InlineData("Authorization", "Setting")]
    [InlineData("Endpoint", "Bearer")]
    [InlineData("aPi\u2003.-_kEy", "Setting")]
    [InlineData("Endpoint", "密码")]
    [InlineData("License", "Setting")]
    [InlineData("SN", "Setting")]
    public void SensitiveNamesShareOneClassificationAndHidePlainValuesAcrossParameterOutputs(string propertyName, string displayName)
    {
        const string plainValue = "ordinary-unstructured-value-7319";
        var property = new CopilotContextProperty { PropertyName = propertyName, Name = displayName, Value = plainValue };

        Assert.True(CopilotBusinessContextBuilder.IsSensitivePropertyName(propertyName)
            || CopilotBusinessContextBuilder.IsSensitivePropertyName(displayName));
        Assert.Equal(CopilotBusinessContextBuilder.IsSensitivePropertyName(propertyName), CopilotMcpAuditLogger.IsSensitiveArgumentName(propertyName));
        Assert.Equal(CopilotBusinessContextBuilder.IsSensitivePropertyName(displayName), CopilotMcpAuditLogger.IsSensitiveArgumentName(displayName));
        var sensitiveName = CopilotBusinessContextBuilder.IsSensitivePropertyName(propertyName) ? propertyName : displayName;
        Assert.Equal("<redacted>", CopilotBusinessContextBuilder.MaskIfSensitive(sensitiveName, plainValue));
        Assert.Equal("<redacted>", CopilotMcpAuditLogger.RedactArgument(sensitiveName, plainValue));
        Assert.Equal("<redacted>", CopilotFlowParameterRedaction.RedactValue(property));

        var content = Serialize(property);

        Assert.DoesNotContain(plainValue, content, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(content);
        var output = Assert.Single(json.RootElement.GetProperty("change").GetProperty("node").GetProperty("parameters").EnumerateArray());
        Assert.Equal("<redacted>", output.GetProperty("value").GetString());
        Assert.Equal(propertyName, property.PropertyName);
        Assert.Equal(displayName, property.Name);
        Assert.Equal(plainValue, property.Value);
    }

    [Theory]
    [InlineData("Gain")]
    [InlineData("IsModified")]
    [InlineData("SnapshotId")]
    [InlineData("IsNew")]
    public void OrdinaryNamesDoNotBecomeSensitiveThroughAnSnSubstringRule(string name)
    {
        const string value = "ordinary-value-retained";
        var property = new CopilotContextProperty { PropertyName = name, Name = name, Value = value };

        Assert.False(CopilotBusinessContextBuilder.IsSensitivePropertyName(name));
        Assert.False(CopilotMcpAuditLogger.IsSensitiveArgumentName(name));
        Assert.Equal(value, CopilotBusinessContextBuilder.MaskIfSensitive(name, value));
        Assert.Equal(value, CopilotMcpAuditLogger.RedactArgument(name, value));
        Assert.Equal(value, CopilotFlowParameterRedaction.RedactValue(property));
        using var json = JsonDocument.Parse(Serialize(property));
        var output = Assert.Single(json.RootElement.GetProperty("change").GetProperty("node").GetProperty("parameters").EnumerateArray());
        Assert.Equal(value, output.GetProperty("value").GetString());
    }

    private static string Serialize(CopilotContextProperty property) => CopilotFlowPatchPreviewFormatter.Serialize(new string('a', 64), new
    {
        operation = "add_node", node = new CopilotFlowNodeContextSnapshot { Parameters = [property] },
    });
}
