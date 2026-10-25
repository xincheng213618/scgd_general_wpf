using ColorVision.ImageEditor.BatchProcessing;
using OpenCvSharp;
using System.IO;
using System.Reflection;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public class BatchImageWindowInputTests
{
    [Fact]
    public void PreparedInputsNormalizeAndDeduplicateWithoutLoadingOrChangingSettings() => StaTest.Run(() =>
    {
        CountingLoader loader = new();
        int algorithmCalls = 0;
        BatchImageAlgorithmDefinition algorithm = new("Test", "_initial", new NoBatchAlgorithmOptions(), _ =>
        {
            algorithmCalls++;
            throw new InvalidOperationException("Input preparation must not execute an algorithm.");
        });
        BatchImageProcessingWindow window = new([algorithm], [loader]);
        try
        {
            string root = Path.Combine(Path.GetTempPath(), "colorvision-prepared-inputs", "source");
            string firstPath = Path.Combine(root, "first.png");
            string secondPath = Path.Combine(root, "second.png");
            Assert.Equal(1, window.AddPreparedInputs([new BatchImageItem(firstPath, Path.Combine(root, "."))]));
            BatchImageItem existing = Assert.Single(window.Files);
            existing.Status = "Existing status";

            TextBox output = (TextBox)window.FindName("OutputDirectoryTextBox");
            TextBox suffix = (TextBox)window.FindName("SuffixTextBox");
            ComboBox format = (ComboBox)window.FindName("OutputFormatComboBox");
            output.Text = Path.Combine(Path.GetTempPath(), "prepared-output");
            suffix.Text = "_chosen";
            format.SelectedIndex = 2;
            object selectedFormat = format.SelectedItem;

            int added = window.AddPreparedInputs(
            [
                new BatchImageItem(firstPath.ToUpperInvariant(), Path.GetDirectoryName(root)),
                new BatchImageItem(Path.Combine(root, "group", "..", "second.png"), Path.Combine(root, ".")),
                new BatchImageItem(secondPath.ToUpperInvariant(), Path.GetDirectoryName(root)),
            ]);

            Assert.Equal(1, added);
            Assert.Equal(2, window.Files.Count);
            Assert.Same(existing, window.Files[0]);
            Assert.Equal(root, existing.SourceRoot);
            Assert.Equal("Existing status", existing.Status);
            Assert.Equal(secondPath, window.Files[1].FilePath);
            Assert.Equal(root, window.Files[1].SourceRoot);
            Assert.Equal(Path.Combine(Path.GetTempPath(), "prepared-output"), output.Text);
            Assert.Equal("_chosen", suffix.Text);
            Assert.Same(selectedFormat, format.SelectedItem);
            Assert.Same(algorithm, ((ComboBox)window.FindName("AlgorithmComboBox")).SelectedItem);
            Assert.Equal(string.Format(ColorVision.ImageEditor.Properties.Resources.BatchFileCount, 2),
                ((TextBlock)window.FindName("FileCountTextBlock")).Text);
            Assert.Equal(0, loader.LoadCalls);
            Assert.Equal(0, algorithmCalls);
        }
        finally
        {
            window.Close();
        }
    });

    [Theory]
    [InlineData("unsupported")]
    [InlineData("invalid-file")]
    [InlineData("invalid-root")]
    [InlineData("outside-root")]
    public void InvalidPreparedInputRejectsTheWholeBatch(string invalidKind) => StaTest.Run(() =>
    {
        CountingLoader loader = new();
        BatchImageProcessingWindow window = CreateWindow(loader);
        try
        {
            string root = Path.Combine(Path.GetTempPath(), "colorvision-prepared-inputs");
            BatchImageItem existing = new(Path.Combine(root, "existing.png"), root);
            Assert.Equal(1, window.AddPreparedInputs([existing]));
            BatchImageItem bad = invalidKind switch
            {
                "unsupported" => new BatchImageItem(Path.Combine(root, "unsupported.txt"), root),
                "invalid-file" => new BatchImageItem("invalid\0.png", root),
                "invalid-root" => new BatchImageItem(Path.Combine(root, "invalid-root.png"), "invalid\0root"),
                _ => new BatchImageItem(Path.Combine(root, "outside.png"), Path.Combine(root, "other")),
            };

            Exception? failure = Record.Exception(() => window.AddPreparedInputs(
                [new BatchImageItem(Path.Combine(root, "valid.png"), root), bad]));

            if (invalidKind == "unsupported") Assert.IsType<NotSupportedException>(failure);
            else Assert.IsAssignableFrom<ArgumentException>(failure);
            Assert.Equal(existing.FilePath, Assert.Single(window.Files).FilePath);
            Assert.Equal(0, loader.LoadCalls);
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public void BusyAndClosedWindowsRejectPreparedInputs() => StaTest.Run(() =>
    {
        BatchImageProcessingWindow window = CreateWindow(new CountingLoader());
        FieldInfo cancellationField = typeof(BatchImageProcessingWindow)
            .GetField("_cancellationTokenSource", BindingFlags.Instance | BindingFlags.NonPublic)!;
        using CancellationTokenSource cancellation = new();
        BatchImageItem[] inputs = [new(Path.Combine(Path.GetTempPath(), "prepared.png"))];
        try
        {
            cancellation.Cancel();
            cancellationField.SetValue(window, cancellation);

            Assert.Throws<InvalidOperationException>(() => window.AddPreparedInputs(inputs));
            Assert.Empty(window.Files);
        }
        finally
        {
            cancellationField.SetValue(window, null);
            window.Close();
        }

        Assert.Throws<InvalidOperationException>(() => window.AddPreparedInputs(inputs));
        Assert.Empty(window.Files);
    });

    [Fact]
    public void PreparedInputsRequireTheOwningDispatcher() => StaTest.Run(() =>
    {
        BatchImageProcessingWindow window = CreateWindow(new CountingLoader());
        try
        {
            Exception? failure = Task.Run(() => Record.Exception(() => window.AddPreparedInputs(
                [new BatchImageItem(Path.Combine(Path.GetTempPath(), "prepared.png"))]))).GetAwaiter().GetResult();

            Assert.IsType<InvalidOperationException>(failure);
            Assert.Empty(window.Files);
        }
        finally
        {
            window.Close();
        }
    });

    private static BatchImageProcessingWindow CreateWindow(IBatchImageLoader loader) => new(
        [new BatchImageAlgorithmDefinition("Test", string.Empty, new NoBatchAlgorithmOptions(), _ => throw new InvalidOperationException())],
        [loader]);

    private sealed class CountingLoader : IBatchImageLoader
    {
        public IReadOnlyCollection<string> Extensions { get; } = [".png"];

        public int LoadCalls { get; private set; }

        public Mat Load(string filePath)
        {
            LoadCalls++;
            throw new InvalidOperationException("Input preparation must not load images.");
        }
    }
}
