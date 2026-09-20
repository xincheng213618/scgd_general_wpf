using System.Windows.Media.Imaging;
namespace HeightMapValidation.Legacy;
// In-memory defaults keep this standalone validation host away from user settings.
public class Window3DConfig
{
    public static Window3DConfig Instance { get; } = new();
    public double DefaultHeightScale { get; set; } = 100;
    public int TargetPixelsX { get; set; } = 512;
    public int TargetPixelsY { get; set; } = 512;
    public string SelectedColormap { get; set; } = "jet";
}
public record ColormapInfo(string Name, BitmapImage? ImageSource, byte[]? Lut);
