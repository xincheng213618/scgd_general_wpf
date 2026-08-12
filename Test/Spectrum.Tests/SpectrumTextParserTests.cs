using ColorVision.Engine.Services.Devices.Spectrum.Correction;
using System.Globalization;

namespace Spectrum.Tests;

public class SpectrumTextParserTests
{
    [Theory]
    [InlineData("380\t1\n381\t2")]
    [InlineData("380,1\n381,2")]
    [InlineData("380;1\n381;2")]
    [InlineData("380 1\n381 2")]
    public void Parse_AcceptsSupportedTwoColumnSeparators(string text)
    {
        SpectrumSeries series = SpectrumTextParser.Parse(text);

        Assert.Equal([380d, 381d], series.Wavelengths);
        Assert.Equal([1d, 2d], series.Values);
    }

    [Theory]
    [InlineData("380,1,2\n381,2,3")]
    [InlineData("380,1\n380,2")]
    [InlineData("381,1\n380,2")]
    [InlineData("380,NaN\n381,2")]
    [InlineData("380,-1\n381,2")]
    public void Parse_RejectsMalformedNonIncreasingOrInvalidData(string text)
    {
        Assert.Throws<FormatException>(() => SpectrumTextParser.Parse(text));
    }

    [Fact]
    public void Parse_PrioritizesExplicitColumnDelimiterWhenCurrentCultureUsesDecimalComma()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            foreach (string text in new[]
                     {
                         "380\t1,5\n381\t2,5",
                         "380;1,5\n381;2,5",
                         "380 1,5\n381 2,5",
                     })
            {
                SpectrumSeries series = SpectrumTextParser.Parse(text);
                Assert.Equal([380d, 381d], series.Wavelengths);
                Assert.Equal([1.5d, 2.5d], series.Values);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
