namespace ColorVision.ImageEditor
{
    public static class CIEColorConverter
    {
        public static double[] RgbToCie1931xy(int r, int g, int b)
        {
            Cie.CieChromaticity xy = Cie.CieColorConverter.RgbToCie1931xy(r, g, b);
            return new[] { xy.X, xy.Y };
        }
    }
}
