using ColorVision.Engine;
using ColorVision.Engine.Templates.Jsons.KB;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using Xunit;

namespace ProjectKB.Tests;

public sealed class KBMeasurementResultsTests
{
    [Fact]
    public void AppliesLegacyAndPairReadingsAndKeepsObservedZero()
    {
        KBItem first = MakeKey("Shift", 30);
        first.KBKeyRect.KBKey.Area = 2;
        first.KBKeyRect.KBKey.KeyScale = 1.5;
        KBItem second = MakeKey("Shift", 45);
        KBItem third = MakeKey("Space", 65);
        (ViewResultAlgType Type, PoiPointResultModel Point)[] points =
        [
            (ViewResultAlgType.POI_Y, MakePoint("Shift", 30, """{"Y":3,"PixNumber":"4"}""")),
            (ViewResultAlgType.POI_Y_V2, MakePoint("Shift", 45, """[{"Y":0,"PixNumber":4.0},null]""")),
            (ViewResultAlgType.POI_Y, MakePoint("Space", 65, """{"Y":"5"}""")),
            (ViewResultAlgType.POI_Y, MakePoint("Unrelated", 25, "invalid JSON")),
        ];

        Assert.Empty(KBMeasurementResults.ApplyLuminance([first, second, third], points, luminanceScale: 2));
        Assert.Equal(18, first.Lv);
        Assert.Equal(0, second.Lv);
        Assert.Equal(10, third.Lv);
    }

    [Theory]
    [InlineData(ViewResultAlgType.POI_Y, "{}")]
    [InlineData(ViewResultAlgType.POI_Y, "{\"Y\":null}")]
    [InlineData(ViewResultAlgType.POI_Y, "{\"Y\":\"NaN\"}")]
    [InlineData(ViewResultAlgType.POI_Y, "{\"Y\":true}")]
    [InlineData(ViewResultAlgType.POI_Y, "{\"Y\":3,\"PixNumber\":true}")]
    [InlineData(ViewResultAlgType.POI_Y, "{\"Y\":3,\"PixNumber\":1.5}")]
    [InlineData(ViewResultAlgType.POI_Y, "{\"Y\":3,\"pixNumber\":\"1.5\"}")]
    [InlineData(ViewResultAlgType.POI_Y, "{\"Y\":0,\"PixNumber\":0}")]
    [InlineData(ViewResultAlgType.POI_Y, "{\"Y\":3,\"PixNumber\":-1}")]
    [InlineData(ViewResultAlgType.POI_Y, "invalid JSON")]
    [InlineData(ViewResultAlgType.POI_Y_V2, "[{\"Y\":0}]")]
    [InlineData(ViewResultAlgType.POI_Y_V2, "[null,{\"Y\":0}]")]
    public void MissingOrMalformedBrightnessRemainsUnmeasured(ViewResultAlgType type, string payload)
    {
        KBItem key = MakeKey("A", 30);
        PoiPointResultModel point = MakePoint("A", 30, payload);

        Assert.Same(key, Assert.Single(KBMeasurementResults.ApplyLuminance([key], [(type, point)], luminanceScale: 1)));
        Assert.Equal(0, key.Lv);
    }

    [Fact]
    public void UnrelatedOrDuplicateReadingsCannotCoverAnUnmeasuredKey()
    {
        KBItem first = MakeKey("A", 30);
        KBItem duplicateIdentity = MakeKey("A", 30);
        KBItem missing = MakeKey("B", 31);
        (ViewResultAlgType Type, PoiPointResultModel Point)[] points =
        [
            (ViewResultAlgType.POI_Y, MakePoint("A", 30, """{"Y":1}""")),
            (ViewResultAlgType.POI_Y, MakePoint("A", 30, """{"Y":2}""")),
            (ViewResultAlgType.POI_Y, MakePoint("B", 30, """{"Y":2}""")),
        ];

        Assert.Equal(
            [duplicateIdentity, missing],
            KBMeasurementResults.ApplyLuminance([first, duplicateIdentity, missing], points, luminanceScale: 1));
        Assert.Equal([first, duplicateIdentity, missing], KBMeasurementResults.ApplyLuminance([first, duplicateIdentity, missing], [], luminanceScale: 1));
    }

    private static KBItem MakeKey(string name, int width)
        => new() { Name = name, KBKeyRect = new KBKeyRect { Width = width, KBKey = new KBKey() } };

    private static PoiPointResultModel MakePoint(string name, int width, string payload)
        => new() { PoiName = name, PoiWidth = width, Value = payload };
}
