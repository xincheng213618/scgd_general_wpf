using ColorVision.UI.Tests;
using ImageProjector;
using OpenCvSharp;
using Pattern.QuadrantGrating;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Pattern.Tests;

public sealed class PatternMigrationTests
{
    private static readonly string[] EditorResourceSources =
    [
        "/HandyControl;component/Themes/basic/colors/colors.xaml",
        "/HandyControl;component/Themes/Theme.xaml",
        "/ColorVision.Themes;component/Themes/White.xaml",
        "/ColorVision.Themes;component/Themes/Base.xaml"
    ];

    [Fact]
    public void DefaultGratingKeepsOriginalTwoByTwoPixelPattern()
    {
        var pattern = new PatternQuadrantGrating();
        Assert.Equal(GratingLayoutMode.ByGridCount, pattern.Config.LayoutMode);
        Assert.Equal(2, pattern.Config.Columns);
        Assert.Equal(2, pattern.Config.Rows);
        Assert.Equal(2, pattern.Config.LineWidth);
        using var image = pattern.Gen(480, 640);
        AssertGrid(image, 2, 2, 2);
    }

    [Theory]
    [InlineData(3, 2, 37, 29, 3)]
    [InlineData(1, 4, 23, 17, 1)]
    [InlineData(7, 5, 7, 5, 2)]
    public void CountLayoutCoversEveryPixel(int columns, int rows, int width, int height, int lineWidth)
    {
        var pattern = new PatternQuadrantGrating();
        pattern.Config.Columns = columns;
        pattern.Config.Rows = rows;
        pattern.Config.LineWidth = lineWidth;
        using var image = pattern.Gen(height, width);
        AssertGrid(image, columns, rows, lineWidth);
    }

    [Fact]
    public void PixelLayoutIncludesPartialRightAndBottomCells()
    {
        var pattern = new PatternQuadrantGrating();
        pattern.Config.LayoutMode = GratingLayoutMode.ByCellSize;
        pattern.Config.CellWidth = 7;
        pattern.Config.CellHeight = 5;
        using var image = pattern.Gen(17, 23);
        for (int y = 0; y < 17; y++)
        for (int x = 0; x < 23; x++)
        {
            int position = (x / 7 + y / 5) % 2 == 0 ? y % 5 : x % 7;
            byte expected = position / 2 % 2 == 0 ? (byte)255 : (byte)0;
            Assert.Equal(new Vec3b(expected, expected, expected), image.At<Vec3b>(y, x));
        }
    }

    [Theory]
    [InlineData(PatternSizeMode.ByFieldOfView)]
    [InlineData(PatternSizeMode.ByPixelSize)]
    public void FieldOfViewCentersGratingAndRetainsThreeColors(PatternSizeMode mode)
    {
        var pattern = new PatternQuadrantGrating();
        pattern.Config.SizeMode = mode;
        pattern.Config.FieldOfViewX = .5;
        pattern.Config.FieldOfViewY = .5;
        pattern.Config.PixelWidth = 8;
        pattern.Config.PixelHeight = 6;
        pattern.Config.MainBrush = Brushes.Red;
        pattern.Config.AltBrush = Brushes.Lime;
        pattern.Config.BackGroundBrush = Brushes.Blue;
        using var image = pattern.Gen(12, 16);
        Assert.Equal(new Vec3b(255, 0, 0), image.At<Vec3b>(0, 0));
        Assert.Equal(new Vec3b(255, 0, 0), image.At<Vec3b>(11, 15));
        Assert.Equal(new Vec3b(0, 255, 0), image.At<Vec3b>(3, 4));
        Assert.Equal(new Vec3b(0, 0, 255), image.At<Vec3b>(5, 4));
        Assert.Equal(new Vec3b(0, 0, 255), image.At<Vec3b>(3, 10));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 10)]
    public void InvalidImageDimensionsFailBeforeAllocation(int height, int width)
    {
        var pattern = new PatternQuadrantGrating();
        Assert.Throws<ArgumentOutOfRangeException>(() => pattern.Gen(height, width));
    }

    [Fact]
    public void OldJsonWithoutLayoutPropertiesPreservesDefaultAndIgnoresDerivedTags()
    {
        var pattern = new PatternQuadrantGrating();
        pattern.SetConfig("{\"LineWidth\":3,\"MainBrushTag\":\"legacy\",\"AltBrushTag\":\"legacy\"}");
        Assert.Equal(3, pattern.Config.LineWidth);
        Assert.Equal(2, pattern.Config.Columns);
        Assert.Equal(2, pattern.Config.Rows);
        Assert.Equal("K", pattern.Config.MainBrushTag);
        Assert.Equal("W", pattern.Config.AltBrushTag);
        Assert.False(TypeDescriptor.GetProperties(pattern.Config)[nameof(pattern.Config.MainBrushTag)]!.IsBrowsable);
        Assert.Equal("G", Brushes.Lime.ToColorTag());
        Assert.Equal("#80123456", new SolidColorBrush(Color.FromArgb(128, 18, 52, 86)).ToColorTag());
    }

    [Fact]
    public void PropertyEditorBindsToCurrentUiAndQuickColorsStillWork()
    {
        WpfTestHost.Invoke(() =>
        {
            foreach (string source in EditorResourceSources)
                System.Windows.Application.Current.Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary { Source = new Uri(source, UriKind.Relative) });
            var config = new PatternQuadrantGratingConfig();
            var editor = new QuadrantGratingEditor(config);
            Assert.Same(config, editor.DataContext);
            var property = typeof(PatternQuadrantGratingConfig).GetProperty(nameof(config.MainBrush))!;
            var panel = new PatternBrushPropertiesEditor().GenProperties(property, config);
            var grid = Assert.IsType<UniformGrid>(panel.Children[1]);
            Assert.Equal(6, grid.Children.Count);
            var greenButton = Assert.IsType<Button>(grid.Children[2]);
            greenButton.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(Colors.Lime, config.MainBrush.Color);
        });
    }

    [Fact]
    public void MigrationRetainsAssembliesEntryPointsConfigTypesAndDefaultDirectories()
    {
        Assert.Equal("Pattern", typeof(PatternQuadrantGrating).Assembly.GetName().Name);
        Assert.Equal("ImageProjector", typeof(ImageProjectorConfig).Assembly.GetName().Name);
        Assert.NotNull(typeof(Pattern.App).Assembly.EntryPoint);
        Assert.NotNull(typeof(ImageProjector.App).Assembly.EntryPoint);
        Assert.Equal("Pattern.PatternManagerConfig", typeof(PatternManagerConfig).FullName);
        Assert.Equal("ImageProjector.ImageProjectorConfig", typeof(ImageProjectorConfig).FullName);
        Assert.Equal("ColorVision", typeof(Pattern.App).Assembly.GetCustomAttribute<AssemblyCompanyAttribute>()!.Company);
        var config = new PatternManagerConfig();
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ColorVision", "Pattern"), config.PatternPath);
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Pattern"), config.SaveFilePath);
        Assert.Equal(Stretch.UniformToFill, ImageProjectorConfig.ToStretch(ImageStretchMode.UniformToFill));
        Assert.Equal(11, typeof(IPattern).Assembly.GetTypes().Count(t => !t.IsAbstract && typeof(IPattern).IsAssignableFrom(t)));
    }

    private static void AssertGrid(Mat image, int columns, int rows, int lineWidth)
    {
        int width = image.Width;
        int height = image.Height;
        for (int row = 0; row < rows; row++)
        for (int column = 0; column < columns; column++)
        {
            int top = row * height / rows;
            int left = column * width / columns;
            for (int y = top; y < (row + 1) * height / rows; y++)
            for (int x = left; x < (column + 1) * width / columns; x++)
            {
                int position = (row + column) % 2 == 0 ? y - top : x - left;
                byte expected = position / lineWidth % 2 == 0 ? (byte)255 : (byte)0;
                Assert.Equal(new Vec3b(expected, expected, expected), image.At<Vec3b>(y, x));
            }
        }
    }
}
