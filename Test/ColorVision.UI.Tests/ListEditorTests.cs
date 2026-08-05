#pragma warning disable CA1707,CA1711,CA1852
using System.ComponentModel;
using System.Globalization;
using ColorVision.UI.PropertyEditor.Editor.List;
using FlowEngineLib.Node.Algorithm;

namespace ColorVision.UI.Tests;

public class ListEditorTests
{
    public enum TestEnum
    {
        Value1,
        Value2,
        Value3
    }

    [Fact]
    public void JsonNumericListConverter_ConvertBack_WithEnumArray_ReturnsArray()
    {
        var converter = new JsonNumericListConverter();

        var result = Assert.IsType<TestEnum[]>(converter.ConvertBack("[0,2]", typeof(TestEnum[]), null!, CultureInfo.InvariantCulture));

        Assert.Equal(new[] { TestEnum.Value1, TestEnum.Value3 }, result);
    }

    [Fact]
    public void JsonNumericListConverter_ConvertBack_WithPointFloatArray_ReturnsArray()
    {
        var converter = new JsonNumericListConverter();

        var result = Assert.IsType<PointFloat[]>(converter.ConvertBack("[{\"X\":1.5,\"Y\":2.25},{\"X\":3.5,\"Y\":4.25}]", typeof(PointFloat[]), null!, CultureInfo.InvariantCulture));

        Assert.Equal(2, result.Length);
        Assert.Equal(1.5f, result[0].X);
        Assert.Equal(2.25f, result[0].Y);
        Assert.Equal(3.5f, result[1].X);
        Assert.Equal(4.25f, result[1].Y);
    }
}
