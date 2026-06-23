using QRCoder;
using QRCoder.Xaml;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.UI.Desktop.LanRemote
{
    internal static class LanRemoteQrCode
    {
        public static DrawingImage Create(string content)
        {
            using QRCodeGenerator generator = new();
            using QRCodeData data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
            XamlQRCode qrCode = new(data);
            DrawingImage image = qrCode.GetGraphic(new Size(220, 220), Brushes.Black, Brushes.White, true);
            image.Freeze();
            return image;
        }
    }
}
