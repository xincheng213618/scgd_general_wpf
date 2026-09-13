using System;

namespace ColorVision.ImageEditor.EditorTools.Filters
{
    internal static class DisplayShaderWhiteBalance
    {
        private static readonly double TemperatureStrength = Math.Log(1.5);
        private static readonly double TintStrength = Math.Log(1.25);

        internal static (double Red, double Green, double Blue) GetGains(double temperature, double tint)
        {
            temperature = Math.Clamp(temperature, -1, 1);
            tint = Math.Clamp(tint, -1, 1);

            return (
                Math.Exp(temperature * TemperatureStrength + tint * TintStrength),
                Math.Exp(-2 * tint * TintStrength),
                Math.Exp(-temperature * TemperatureStrength + tint * TintStrength));
        }
    }
}
