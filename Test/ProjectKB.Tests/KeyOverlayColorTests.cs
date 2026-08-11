using System.Windows.Media;
using Xunit;

namespace ProjectKB.Tests;

public class KeyOverlayColorTests
{
    [Fact]
    public void FailedKeyUsesRedThickOutline()
    {
        KBItem failed = new() { Result = false };

        Pen pen = ProjectKBWindow.CreateDefaultKeyPen(failed, failed, failed);

        AssertPen(pen, Colors.Red, 10);
    }

    [Fact]
    public void DarkestPassingKeyUsesVioletThickOutline()
    {
        KBItem darkest = new();
        KBItem brightest = new();

        Pen pen = ProjectKBWindow.CreateDefaultKeyPen(darkest, darkest, brightest);

        AssertPen(pen, Colors.Violet, 10);
    }

    [Fact]
    public void BrightestPassingKeyUsesWhiteThickOutline()
    {
        KBItem darkest = new();
        KBItem brightest = new();

        Pen pen = ProjectKBWindow.CreateDefaultKeyPen(brightest, darkest, brightest);

        AssertPen(pen, Colors.White, 10);
    }

    [Fact]
    public void DarkestTakesPriorityWhenPassingKeyIsAlsoBrightest()
    {
        KBItem onlyPassingKey = new();

        Pen pen = ProjectKBWindow.CreateDefaultKeyPen(onlyPassingKey, onlyPassingKey, onlyPassingKey);

        AssertPen(pen, Colors.Violet, 10);
    }

    [Fact]
    public void OrdinaryPassingKeyUsesGrayThinOutline()
    {
        KBItem ordinary = new();

        Pen pen = ProjectKBWindow.CreateDefaultKeyPen(ordinary, new KBItem(), new KBItem());

        AssertPen(pen, Colors.Gray, 5);
    }

    [Fact]
    public void SelectedKeyUsesLimeThickOutline()
    {
        Pen pen = ProjectKBWindow.CreateSelectedKeyPen();

        AssertPen(pen, Colors.Lime, 12);
    }

    private static void AssertPen(Pen pen, Color expectedColor, double expectedThickness)
    {
        SolidColorBrush brush = Assert.IsType<SolidColorBrush>(pen.Brush);
        Assert.Equal(expectedColor, brush.Color);
        Assert.Equal(expectedThickness, pen.Thickness);
    }
}
