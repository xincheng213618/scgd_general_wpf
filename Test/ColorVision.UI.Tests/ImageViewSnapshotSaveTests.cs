using ColorVision.ImageEditor;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class ImageViewSnapshotSaveTests
{
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
}
