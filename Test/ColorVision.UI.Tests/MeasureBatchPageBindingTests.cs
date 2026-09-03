using System.IO;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace ColorVision.UI.Tests;

public sealed class MeasureBatchPageBindingTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Fact]
    public void ResultCountRunsUseOneWayBindings()
    {
        XDocument document = LoadPage();
        string[] countBindings = document
            .Descendants(Presentation + "Run")
            .Select(run => run.Attribute("Text")?.Value)
            .Where(text => text?.Contains("Items.Count", StringComparison.Ordinal) == true)
            .Cast<string>()
            .ToArray();

        Assert.Equal(2, countBindings.Length);
        Assert.Contains(countBindings, binding => binding.Contains("ElementName=listView1", StringComparison.Ordinal));
        Assert.Contains(countBindings, binding => binding.Contains("ElementName=listView2", StringComparison.Ordinal));
        Assert.All(countBindings, binding => Assert.Contains("Mode=OneWay", binding, StringComparison.Ordinal));
    }

    private static XDocument LoadPage([CallerFilePath] string testPath = "")
        => XDocument.Load(Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(testPath)!,
            "..", "..", "Engine", "ColorVision.Engine", "Dao", "MeasureBatchPage.xaml")));
}
