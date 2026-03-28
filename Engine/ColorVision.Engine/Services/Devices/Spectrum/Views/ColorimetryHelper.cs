using System;

namespace ColorVision.Engine.Services.Devices.Spectrum.Views
{
    public static class ColorimetryHelper
    {
        // 1931 2度视角光谱轨迹坐标表 (380nm - 780nm, 5nm间隔)
        private static readonly double[][] SpectrumLocus = new double[][]
        {
            new[] { 380, 0.1741, 0.0050 }, new[] { 385, 0.1740, 0.0050 }, new[] { 390, 0.1738, 0.0049 }, new[] { 395, 0.1736, 0.0049 },
            new[] { 400, 0.1733, 0.0048 }, new[] { 405, 0.1730, 0.0048 }, new[] { 410, 0.1726, 0.0048 }, new[] { 415, 0.1721, 0.0048 },
            new[] { 420, 0.1714, 0.0051 }, new[] { 425, 0.1703, 0.0058 }, new[] { 430, 0.1689, 0.0069 }, new[] { 435, 0.1669, 0.0086 },
            new[] { 440, 0.1644, 0.0109 }, new[] { 445, 0.1611, 0.0138 }, new[] { 450, 0.1566, 0.0177 }, new[] { 455, 0.1510, 0.0227 },
            new[] { 460, 0.1440, 0.0297 }, new[] { 465, 0.1355, 0.0399 }, new[] { 470, 0.1241, 0.0578 }, new[] { 475, 0.1096, 0.0868 },
            new[] { 480, 0.0913, 0.1327 }, new[] { 485, 0.0687, 0.2007 }, new[] { 490, 0.0454, 0.2950 }, new[] { 495, 0.0235, 0.4127 },
            new[] { 500, 0.0082, 0.5384 }, new[] { 505, 0.0039, 0.6548 }, new[] { 510, 0.0139, 0.7502 }, new[] { 515, 0.0389, 0.8120 },
            new[] { 520, 0.0743, 0.8338 }, new[] { 525, 0.1142, 0.8262 }, new[] { 530, 0.1547, 0.8059 }, new[] { 535, 0.1929, 0.7816 },
            new[] { 540, 0.2296, 0.7543 }, new[] { 545, 0.2658, 0.7243 }, new[] { 550, 0.3016, 0.6923 }, new[] { 555, 0.3374, 0.6589 },
            new[] { 560, 0.3731, 0.6245 }, new[] { 565, 0.4087, 0.5896 }, new[] { 570, 0.4441, 0.5547 }, new[] { 575, 0.4788, 0.5202 },
            new[] { 580, 0.5125, 0.4866 }, new[] { 585, 0.5448, 0.4544 }, new[] { 590, 0.5752, 0.4242 }, new[] { 595, 0.6029, 0.3965 },
            new[] { 600, 0.6270, 0.3725 }, new[] { 605, 0.6482, 0.3514 }, new[] { 610, 0.6658, 0.3340 }, new[] { 615, 0.6801, 0.3197 },
            new[] { 620, 0.6915, 0.3083 }, new[] { 625, 0.7006, 0.2993 }, new[] { 630, 0.7079, 0.2920 }, new[] { 635, 0.7140, 0.2859 },
            new[] { 640, 0.7190, 0.2809 }, new[] { 645, 0.7230, 0.2770 }, new[] { 650, 0.7260, 0.2740 }, new[] { 655, 0.7283, 0.2717 },
            new[] { 660, 0.7300, 0.2700 }, new[] { 665, 0.7311, 0.2689 }, new[] { 670, 0.7320, 0.2680 }, new[] { 675, 0.7327, 0.2673 },
            new[] { 680, 0.7334, 0.2666 }, new[] { 685, 0.7340, 0.2660 }, new[] { 690, 0.7344, 0.2656 }, new[] { 695, 0.7346, 0.2654 },
            new[] { 700, 0.7347, 0.2653 }, new[] { 705, 0.7347, 0.2653 }, new[] { 710, 0.7347, 0.2653 }, new[] { 715, 0.7347, 0.2653 },
            new[] { 720, 0.7347, 0.2653 }, new[] { 725, 0.7347, 0.2653 }, new[] { 730, 0.7347, 0.2653 }, new[] { 735, 0.7347, 0.2653 },
            new[] { 740, 0.7347, 0.2653 }, new[] { 745, 0.7347, 0.2653 }, new[] { 750, 0.7347, 0.2653 }, new[] { 755, 0.7347, 0.2653 },
            new[] { 760, 0.7347, 0.2653 }, new[] { 765, 0.7347, 0.2653 }, new[] { 770, 0.7347, 0.2653 }, new[] { 775, 0.7347, 0.2653 },
            new[] { 780, 0.7347, 0.2653 }
        };

        /// <summary>
        /// 使用 McCamy 近似公式验证/计算相关色温 (CCT)
        /// </summary>
        public static double CalculateCCT(double x, double y)
        {
            if (x == 0 && y == 0) return 0;
            double n = (x - 0.3320) / (0.1858 - y);
            return 449.0 * Math.Pow(n, 3) + 3525.0 * Math.Pow(n, 2) + 6823.3 * n + 5520.33;
        }

        /// <summary>
        /// 计算主波长 (Dominant Wavelength)
        /// </summary>
        public static double CalculateDominantWavelength(double x, double y, double refX = 0.3127, double refY = 0.3290)
        {
            double vx = x - refX;
            double vy = y - refY;

            if (Math.Abs(vx) < 1e-6 && Math.Abs(vy) < 1e-6)
                return -1;

            for (int i = 0; i < SpectrumLocus.Length - 1; i++)
            {
                double v1x = SpectrumLocus[i][1] - refX;
                double v1y = SpectrumLocus[i][2] - refY;

                double v2x = SpectrumLocus[i + 1][1] - refX;
                double v2y = SpectrumLocus[i + 1][2] - refY;

                double cross1 = v1x * vy - v1y * vx;
                double cross2 = vx * v2y - vy * v2x;

                double dot1 = v1x * vx + v1y * vy;
                double dot2 = v2x * vx + v2y * vy;

                if (((cross1 >= 0 && cross2 >= 0) || (cross1 <= 0 && cross2 <= 0)) && (dot1 > 0 || dot2 > 0))
                {
                    if (Math.Abs(cross1 + cross2) < 1e-10) return SpectrumLocus[i][0];

                    double ratio = cross1 / (cross1 + cross2);
                    return Math.Round(SpectrumLocus[i][0] + ratio * (SpectrumLocus[i + 1][0] - SpectrumLocus[i][0]), 2);
                }
            }

            return -1;
        }

        /// <summary>
        /// 计算兴奋纯度 (Excitation Purity)
        /// </summary>
        public static double CalculateExcitationPurity(double x, double y, double dominantWavelength, double refX = 0.3127, double refY = 0.3290)
        {
            if (double.IsNaN(x) || double.IsNaN(y) || dominantWavelength < 380 || dominantWavelength > 780)
                return 0;

            double dx = x - refX;
            double dy = y - refY;
            double distSample = Math.Sqrt(dx * dx + dy * dy);

            if (distSample < 1e-10) return 0;

            GetSpectrumLocusPoint(dominantWavelength, out double xd, out double yd);
            double dxd = xd - refX;
            double dyd = yd - refY;
            double distDominant = Math.Sqrt(dxd * dxd + dyd * dyd);

            if (distDominant < 1e-10) return 0;

            double purity = Math.Round(distSample / distDominant, 4);
            return purity > 1.0 ? 1.0 : purity;
        }

        private static void GetSpectrumLocusPoint(double wavelength, out double x, out double y)
        {
            double wl = Math.Max(380, Math.Min(780, wavelength));

            for (int i = 0; i < SpectrumLocus.Length - 1; i++)
            {
                if (wl >= SpectrumLocus[i][0] && wl <= SpectrumLocus[i + 1][0])
                {
                    double t = (wl - SpectrumLocus[i][0]) / (SpectrumLocus[i + 1][0] - SpectrumLocus[i][0]);
                    x = SpectrumLocus[i][1] + t * (SpectrumLocus[i + 1][1] - SpectrumLocus[i][1]);
                    y = SpectrumLocus[i][2] + t * (SpectrumLocus[i + 1][2] - SpectrumLocus[i][2]);
                    return;
                }
            }

            x = SpectrumLocus[^1][1];
            y = SpectrumLocus[^1][2];
        }
    }
}
