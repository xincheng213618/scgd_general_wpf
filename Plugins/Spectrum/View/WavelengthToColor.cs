using System.Windows.Media;

namespace Spectrum
{
    /// <summary>
    /// Converts visible light wavelength (380-780nm) to RGB color.
    /// Based on CIE color matching approximation by Dan Bruton.
    /// </summary>
    public static class WavelengthToColor
    {
        /// <summary>
        /// Convert a wavelength in nm to a WPF Color.
        /// Returns Colors.Transparent for wavelengths outside the visible range.
        /// </summary>
        public static Color Convert(double wavelength)
        {
            double r, g, b;

            if (wavelength >= 380 && wavelength < 440)
            {
                r = -(wavelength - 440) / (440 - 380);
                g = 0.0;
                b = 1.0;
            }
            else if (wavelength >= 440 && wavelength < 490)
            {
                r = 0.0;
                g = (wavelength - 440) / (490 - 440);
                b = 1.0;
            }
            else if (wavelength >= 490 && wavelength < 510)
            {
                r = 0.0;
                g = 1.0;
                b = -(wavelength - 510) / (510 - 490);
            }
            else if (wavelength >= 510 && wavelength < 580)
            {
                r = (wavelength - 510) / (580 - 510);
                g = 1.0;
                b = 0.0;
            }
            else if (wavelength >= 580 && wavelength < 645)
            {
                r = 1.0;
                g = -(wavelength - 645) / (645 - 580);
                b = 0.0;
            }
            else if (wavelength >= 645 && wavelength <= 780)
            {
                r = 1.0;
                g = 0.0;
                b = 0.0;
            }
            else
            {
                return Colors.Transparent;
            }

            // Intensity attenuation at edges of visible spectrum
            double factor;
            if (wavelength >= 380 && wavelength < 420)
                factor = 0.3 + 0.7 * (wavelength - 380) / (420 - 380);
            else if (wavelength >= 700 && wavelength <= 780)
                factor = 0.3 + 0.7 * (780 - wavelength) / (780 - 700);
            else
                factor = 1.0;

            r = Math.Pow(r * factor, 0.8);
            g = Math.Pow(g * factor, 0.8);
            b = Math.Pow(b * factor, 0.8);

            return Color.FromRgb(
                (byte)(r * 255),
                (byte)(g * 255),
                (byte)(b * 255));
        }

        /// <summary>
        /// Convert a wavelength in nm to a SolidColorBrush.
        /// </summary>
        public static SolidColorBrush ToBrush(double wavelength)
        {
            var brush = new SolidColorBrush(Convert(wavelength));
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// Convert a wavelength in nm to a hex color string (#RRGGBB).
        /// </summary>
        public static string ToHex(double wavelength)
        {
            var color = Convert(wavelength);
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
    }
}
