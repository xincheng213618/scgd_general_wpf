using System.Windows;
using System.Windows.Shell;

#if SPECTRUM_ABOUT
namespace Spectrum.Help.Art;
#else
namespace ColorVision.UI.Views.About;
#endif

/// <summary>Sizes an opaque exhibition window and aligns its native corners with its scaled artwork.</summary>
public static class AboutWindowChrome
{
    /// <summary>Call once after InitializeComponent, while Width and Height still describe the artwork.</summary>
    public static void FitToWorkArea(Window window, double visualCornerRadius)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentOutOfRangeException.ThrowIfNegative(visualCornerRadius);

        double scale = Math.Min(1, Math.Min((SystemParameters.WorkArea.Width - 40) / window.Width, (SystemParameters.WorkArea.Height - 40) / window.Height));
        if (scale > 0)
        {
            window.Width *= scale;
            window.Height *= scale;
        }
        else
        {
            scale = 1;
        }

        // WindowChrome passes this value to CreateRoundRectRgn as an ellipse diameter.
        // Its DPI conversion is automatic; only the Viewbox's layout scale belongs here.
        WindowChrome.SetWindowChrome(window, new WindowChrome
        {
            CaptionHeight = 0,
            CornerRadius = new CornerRadius(2 * visualCornerRadius * scale),
            GlassFrameThickness = new Thickness(0),
            ResizeBorderThickness = new Thickness(0),
            UseAeroCaptionButtons = false
        });
    }
}
