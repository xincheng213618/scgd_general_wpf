using ColorVision.ImageEditor;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class ImageViewSnapshotSaveTests
{
    [Fact]
    public async Task SaveSnapshotAsync_ComposesFrozenSceneOnBackgroundStaThread()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "ColorVision.Tests",
            Guid.NewGuid().ToString("N"));
        string fileName = Path.Combine(directory, "background-scene.png");
        try
        {
            DrawingGroup scene = new();
            scene.Children.Add(new GeometryDrawing(
                Brushes.Blue,
                null,
                new RectangleGeometry(new Rect(0, 0, 4, 4))));
            scene.Children.Add(new GeometryDrawing(
                Brushes.Red,
                null,
                new RectangleGeometry(new Rect(1, 1, 2, 2))));
            ImageViewSnapshot snapshot = ImageViewSnapshot.Create(scene, 4, 4);

            await ImageView.SaveSnapshotAsync(snapshot, fileName);

            BitmapFrame frame;
            using (FileStream stream = File.OpenRead(fileName))
            {
                PngBitmapDecoder decoder = new(
                    stream,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);
                frame = decoder.Frames[0];
                frame.Freeze();
            }

            Assert.Equal(4, frame.PixelWidth);
            Assert.Equal(4, frame.PixelHeight);
            AssertPixel(frame, 0, 0, blue: 255, green: 0, red: 0, alpha: 255);
            AssertPixel(frame, 2, 2, blue: 0, green: 0, red: 255, alpha: 255);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveSnapshotAsync_WritesFrozenBitmapAsPng()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "ColorVision.Tests",
            Guid.NewGuid().ToString("N"));
        string fileName = Path.Combine(directory, "snapshot.png");
        try
        {
            byte[] pixels =
            [
                0x00, 0x00, 0xFF, 0xFF,
                0x00, 0xFF, 0x00, 0xFF,
                0xFF, 0x00, 0x00, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF,
            ];
            BitmapSource snapshot = BitmapSource.Create(
                2,
                2,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                pixels,
                8);
            snapshot.Freeze();

            await ImageView.SaveSnapshotAsync(snapshot, fileName);

            byte[] signature = new byte[8];
            using FileStream stream = File.OpenRead(fileName);
            int bytesRead = await stream.ReadAsync(signature);
            Assert.Equal(signature.Length, bytesRead);
            Assert.Equal(
                new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 },
                signature);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static void AssertPixel(
        BitmapSource source,
        int x,
        int y,
        byte blue,
        byte green,
        byte red,
        byte alpha)
    {
        FormatConvertedBitmap converted = new(source, PixelFormats.Bgra32, null, 0);
        byte[] pixel = new byte[4];
        converted.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        Assert.Equal(new[] { blue, green, red, alpha }, pixel);
    }

}
