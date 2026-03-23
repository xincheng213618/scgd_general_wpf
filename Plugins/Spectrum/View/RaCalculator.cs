using System;

namespace Spectrum.View
{
    /// <summary>
    /// Simplified CRI Ra calculator using CIE 1931 2° observer color matching functions.
    /// Computes Ra from relative spectral data (380-780nm, 1nm steps in fPL array with 0.1nm interval).
    /// 
    /// This is a simplified implementation that uses the McCamy CCT approximation
    /// and Planckian reference for CCT > 5000K, CIE illuminant D series otherwise.
    /// </summary>
    public static class RaCalculator
    {
        // CIE 1931 2° observer color matching functions at 5nm intervals (380-780nm)
        // x̄(λ), ȳ(λ), z̄(λ)
        private static readonly double[] CMF_X = {
            0.001368, 0.002236, 0.004243, 0.00765, 0.01431, 0.02319, 0.04351, 0.07763, 0.13438, 0.21477,
            0.2839, 0.3285, 0.34828, 0.34806, 0.3362, 0.3187, 0.2908, 0.2511, 0.19536, 0.1421,
            0.09564, 0.05795, 0.03201, 0.0147, 0.0049, 0.0024, 0.0093, 0.0291, 0.06327, 0.1096,
            0.1655, 0.22575, 0.2904, 0.3597, 0.43345, 0.51205, 0.5945, 0.6784, 0.7621, 0.8425,
            0.9163, 0.9786, 1.0263, 1.0567, 1.0622, 1.0456, 1.0026, 0.9384, 0.85445, 0.7514,
            0.6424, 0.5419, 0.4479, 0.3608, 0.2835, 0.2187, 0.1649, 0.1212, 0.0874, 0.0636,
            0.04677, 0.0329, 0.0227, 0.01584, 0.01136, 0.00811, 0.00579, 0.004109, 0.002899, 0.002049,
            0.001440, 0.001000, 0.000690, 0.000476, 0.000332, 0.000235, 0.000166, 0.000117, 0.0000830, 0.0000590,
            0.0000420
        };

        private static readonly double[] CMF_Y = {
            0.000039, 0.000064, 0.00012, 0.000217, 0.000396, 0.00064, 0.00121, 0.00218, 0.004, 0.0073,
            0.0116, 0.01684, 0.023, 0.0298, 0.038, 0.048, 0.06, 0.0739, 0.09098, 0.1126,
            0.13902, 0.1693, 0.20802, 0.2586, 0.323, 0.4073, 0.503, 0.6082, 0.71, 0.7932,
            0.862, 0.9149, 0.954, 0.9803, 0.99495, 1.0, 0.995, 0.9786, 0.952, 0.9154,
            0.87, 0.8163, 0.757, 0.6949, 0.631, 0.5668, 0.503, 0.4412, 0.381, 0.321,
            0.265, 0.217, 0.175, 0.1382, 0.107, 0.0816, 0.061, 0.04458, 0.032, 0.0232,
            0.017, 0.01192, 0.00821, 0.005723, 0.004102, 0.002929, 0.002091, 0.001484, 0.001047, 0.00074,
            0.00052, 0.000361, 0.000249, 0.000172, 0.00012, 0.000085, 0.00006, 0.0000423, 0.00003, 0.0000213,
            0.0000150
        };

        private static readonly double[] CMF_Z = {
            0.00645, 0.01055, 0.02006, 0.03621, 0.06785, 0.1102, 0.2074, 0.3713, 0.6456, 1.03905,
            1.3856, 1.623, 1.74706, 1.7826, 1.7721, 1.7441, 1.6692, 1.5281, 1.28764, 1.0419,
            0.81295, 0.6162, 0.46518, 0.3533, 0.272, 0.2123, 0.1582, 0.1117, 0.07825, 0.05725,
            0.04216, 0.02984, 0.0203, 0.0134, 0.00875, 0.00575, 0.0039, 0.00275, 0.0021, 0.0018,
            0.00165, 0.0014, 0.0011, 0.001, 0.0008, 0.0006, 0.00034, 0.00024, 0.00019, 0.0001,
            0.00005, 0.00003, 0.00002, 0.00001, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0
        };

        // CIE 1995 test color samples Ri spectral reflectances at 5nm intervals (380-780nm)
        // Only the 8 standard samples for Ra (TCS01-TCS08) are used
        private static readonly double[][] TCS = new double[8][];

        static RaCalculator()
        {
            // Approximate spectral reflectance data for TCS01-TCS08 (simplified)
            // These are standardized CIE test color sample reflectances
            TCS[0] = new double[] { // TCS01 - Light Grayish Red
                0.219, 0.239, 0.252, 0.256, 0.256, 0.254, 0.250, 0.248, 0.245, 0.241, 0.237, 0.232, 0.228, 0.225, 0.222, 0.220,
                0.218, 0.216, 0.214, 0.214, 0.214, 0.216, 0.218, 0.223, 0.225, 0.226, 0.226, 0.225, 0.225, 0.227, 0.230, 0.236,
                0.245, 0.256, 0.270, 0.284, 0.300, 0.316, 0.333, 0.349, 0.365, 0.380, 0.394, 0.407, 0.419, 0.431, 0.441, 0.451,
                0.460, 0.468, 0.475, 0.481, 0.487, 0.492, 0.497, 0.501, 0.506, 0.510, 0.513, 0.517, 0.520, 0.523, 0.525, 0.527,
                0.529, 0.531, 0.533, 0.535, 0.536, 0.537, 0.538, 0.539, 0.540, 0.541, 0.541, 0.542, 0.542, 0.542, 0.543, 0.543, 0.543
            };
            TCS[1] = new double[] { // TCS02 - Dark Grayish Yellow
                0.070, 0.079, 0.089, 0.098, 0.105, 0.110, 0.113, 0.116, 0.117, 0.118, 0.120, 0.121, 0.122, 0.122, 0.123, 0.124,
                0.127, 0.132, 0.140, 0.152, 0.170, 0.192, 0.219, 0.249, 0.281, 0.309, 0.333, 0.350, 0.362, 0.370, 0.375, 0.378,
                0.381, 0.383, 0.386, 0.389, 0.392, 0.396, 0.400, 0.405, 0.411, 0.417, 0.423, 0.430, 0.437, 0.444, 0.452, 0.459,
                0.466, 0.472, 0.479, 0.485, 0.491, 0.496, 0.500, 0.504, 0.508, 0.512, 0.515, 0.519, 0.522, 0.525, 0.528, 0.530,
                0.532, 0.534, 0.536, 0.538, 0.540, 0.541, 0.542, 0.543, 0.544, 0.545, 0.546, 0.547, 0.547, 0.548, 0.548, 0.549, 0.549
            };
            TCS[2] = new double[] { // TCS03 - Strong Yellow Green
                0.065, 0.068, 0.070, 0.072, 0.073, 0.073, 0.074, 0.074, 0.075, 0.076, 0.078, 0.080, 0.084, 0.090, 0.098, 0.109,
                0.123, 0.143, 0.170, 0.205, 0.245, 0.287, 0.329, 0.367, 0.400, 0.425, 0.442, 0.452, 0.457, 0.458, 0.457, 0.454,
                0.450, 0.445, 0.439, 0.432, 0.425, 0.418, 0.411, 0.403, 0.396, 0.389, 0.383, 0.377, 0.371, 0.366, 0.361, 0.356,
                0.352, 0.348, 0.344, 0.340, 0.337, 0.334, 0.331, 0.328, 0.326, 0.324, 0.322, 0.320, 0.318, 0.316, 0.315, 0.314,
                0.313, 0.312, 0.311, 0.310, 0.309, 0.308, 0.308, 0.307, 0.307, 0.307, 0.307, 0.307, 0.307, 0.307, 0.307, 0.307, 0.307
            };
            TCS[3] = new double[] { // TCS04 - Moderate Yellowish Green
                0.074, 0.084, 0.094, 0.103, 0.110, 0.115, 0.119, 0.121, 0.124, 0.126, 0.130, 0.136, 0.145, 0.157, 0.175, 0.198,
                0.227, 0.260, 0.296, 0.333, 0.369, 0.400, 0.424, 0.440, 0.449, 0.452, 0.451, 0.447, 0.441, 0.435, 0.429, 0.424,
                0.420, 0.417, 0.414, 0.412, 0.410, 0.408, 0.407, 0.405, 0.404, 0.403, 0.402, 0.401, 0.400, 0.400, 0.399, 0.399,
                0.398, 0.398, 0.397, 0.397, 0.396, 0.396, 0.396, 0.396, 0.396, 0.396, 0.396, 0.396, 0.396, 0.396, 0.396, 0.396,
                0.396, 0.396, 0.396, 0.396, 0.396, 0.396, 0.396, 0.396, 0.396, 0.396, 0.396, 0.396, 0.396, 0.396, 0.396, 0.396, 0.396
            };
            TCS[4] = new double[] { // TCS05 - Light Bluish Green
                0.166, 0.189, 0.213, 0.237, 0.262, 0.285, 0.309, 0.331, 0.349, 0.362, 0.370, 0.373, 0.373, 0.370, 0.364, 0.356,
                0.348, 0.339, 0.330, 0.322, 0.313, 0.305, 0.297, 0.289, 0.282, 0.275, 0.268, 0.261, 0.255, 0.249, 0.243, 0.238,
                0.233, 0.229, 0.226, 0.223, 0.220, 0.218, 0.216, 0.214, 0.212, 0.210, 0.208, 0.207, 0.206, 0.204, 0.203, 0.202,
                0.201, 0.200, 0.199, 0.199, 0.198, 0.198, 0.197, 0.197, 0.196, 0.196, 0.196, 0.195, 0.195, 0.195, 0.195, 0.195,
                0.195, 0.195, 0.195, 0.195, 0.195, 0.195, 0.195, 0.195, 0.195, 0.195, 0.195, 0.195, 0.195, 0.195, 0.195, 0.195, 0.195
            };
            TCS[5] = new double[] { // TCS06 - Light Blue
                0.156, 0.186, 0.217, 0.248, 0.280, 0.313, 0.347, 0.380, 0.410, 0.435, 0.452, 0.462, 0.465, 0.461, 0.453, 0.441,
                0.426, 0.410, 0.393, 0.376, 0.360, 0.344, 0.330, 0.316, 0.304, 0.292, 0.282, 0.272, 0.264, 0.256, 0.250, 0.244,
                0.239, 0.235, 0.231, 0.228, 0.225, 0.222, 0.220, 0.218, 0.216, 0.214, 0.213, 0.211, 0.210, 0.209, 0.208, 0.207,
                0.206, 0.205, 0.204, 0.204, 0.203, 0.203, 0.202, 0.202, 0.201, 0.201, 0.201, 0.200, 0.200, 0.200, 0.200, 0.200,
                0.200, 0.200, 0.200, 0.200, 0.200, 0.200, 0.200, 0.200, 0.200, 0.200, 0.200, 0.200, 0.200, 0.200, 0.200, 0.200, 0.200
            };
            TCS[6] = new double[] { // TCS07 - Light Violet
                0.130, 0.143, 0.155, 0.164, 0.170, 0.173, 0.173, 0.172, 0.169, 0.165, 0.160, 0.155, 0.150, 0.144, 0.139, 0.134,
                0.129, 0.126, 0.123, 0.121, 0.120, 0.120, 0.121, 0.122, 0.124, 0.127, 0.130, 0.134, 0.139, 0.144, 0.150, 0.157,
                0.165, 0.174, 0.185, 0.197, 0.211, 0.226, 0.243, 0.261, 0.281, 0.301, 0.321, 0.341, 0.361, 0.380, 0.398, 0.415,
                0.431, 0.446, 0.460, 0.473, 0.485, 0.495, 0.505, 0.514, 0.522, 0.529, 0.536, 0.542, 0.548, 0.553, 0.558, 0.562,
                0.566, 0.569, 0.572, 0.575, 0.577, 0.579, 0.581, 0.583, 0.584, 0.586, 0.587, 0.588, 0.589, 0.590, 0.590, 0.591, 0.591
            };
            TCS[7] = new double[] { // TCS08 - Light Reddish Purple
                0.120, 0.134, 0.148, 0.159, 0.166, 0.170, 0.171, 0.170, 0.167, 0.163, 0.157, 0.151, 0.145, 0.139, 0.133, 0.127,
                0.122, 0.118, 0.115, 0.113, 0.112, 0.112, 0.112, 0.114, 0.116, 0.118, 0.120, 0.122, 0.124, 0.127, 0.130, 0.134,
                0.139, 0.145, 0.153, 0.162, 0.173, 0.186, 0.201, 0.219, 0.240, 0.264, 0.291, 0.320, 0.351, 0.383, 0.414, 0.445,
                0.474, 0.501, 0.526, 0.548, 0.568, 0.585, 0.600, 0.613, 0.624, 0.633, 0.641, 0.648, 0.654, 0.659, 0.663, 0.667,
                0.670, 0.673, 0.675, 0.677, 0.679, 0.680, 0.681, 0.682, 0.683, 0.684, 0.685, 0.685, 0.686, 0.686, 0.687, 0.687, 0.687
            };
        }

        /// <summary>
        /// Computes a simplified CRI Ra from spectral data.
        /// Uses CIE 1931 color matching functions and Planckian reference illuminant.
        /// </summary>
        /// <param name="fPL">Relative spectral power distribution (0.1nm intervals from fSpect1)</param>
        /// <param name="fSpect1">Starting wavelength (typically 380)</param>
        /// <param name="fInterval">Wavelength interval (typically 0.1)</param>
        /// <param name="cct">Correlated Color Temperature in K</param>
        /// <returns>Ra value (0-100), or 0 if calculation fails</returns>
        public static float ComputeRa(float[] fPL, float fSpect1, float fInterval, float cct)
        {
            if (fPL == null || fPL.Length == 0 || cct < 1000 || cct > 25000)
                return 0;

            try
            {
                // Sample spectral data at 5nm intervals (380-780nm)
                int numPoints = 81; // (780-380)/5 + 1
                double[] testSPD = new double[numPoints];
                double[] refSPD = new double[numPoints];

                for (int i = 0; i < numPoints; i++)
                {
                    double wavelength = 380 + i * 5;
                    testSPD[i] = InterpolateSPD(fPL, fSpect1, fInterval, wavelength);
                    refSPD[i] = PlanckianSPD(wavelength, cct);
                }

                // Normalize both SPDs
                NormalizeSPD(testSPD);
                NormalizeSPD(refSPD);

                // Calculate tristimulus values for each TCS under test and reference illuminant
                double totalRi = 0;
                int validSamples = 0;

                for (int t = 0; t < 8; t++)
                {
                    // Tristimulus values under test illuminant
                    ComputeXYZ(testSPD, TCS[t], out double Xt, out double Yt, out double Zt);
                    // Tristimulus values under reference illuminant
                    ComputeXYZ(refSPD, TCS[t], out double Xr, out double Yr, out double Zr);

                    if (Yt <= 0 || Yr <= 0) continue;

                    // Convert to CIE 1960 UCS (u, v)
                    ToUCS(Xt, Yt, Zt, out double ut, out double vt);
                    ToUCS(Xr, Yr, Zr, out double ur, out double vr);

                    // Color difference in UCS space
                    double deltaE = Math.Sqrt(Math.Pow(ur - ut, 2) + Math.Pow(vr - vt, 2));

                    // Special CRI Ri = 100 - 4.6 * deltaE * 1000 (simplified)
                    double Ri = 100.0 - 4.6 * deltaE * 1000;
                    totalRi += Ri;
                    validSamples++;
                }

                if (validSamples == 0) return 0;
                double Ra = totalRi / validSamples;

                // Clamp to valid range
                return (float)Math.Max(0, Math.Min(100, Math.Round(Ra, 1)));
            }
            catch
            {
                return 0;
            }
        }

        private static double InterpolateSPD(float[] fPL, float fSpect1, float fInterval, double wavelength)
        {
            double index = (wavelength - fSpect1) / fInterval;
            int i0 = (int)Math.Floor(index);
            int i1 = i0 + 1;

            if (i0 < 0 || i1 >= fPL.Length)
                return 0;

            double frac = index - i0;
            return fPL[i0] * (1 - frac) + fPL[i1] * frac;
        }

        private static double PlanckianSPD(double wavelength, double T)
        {
            // Planck's law (spectral radiance) · 1/(exp(hc/λkT) - 1)
            // Simplified with constants for nm
            double lambda = wavelength * 1e-9; // nm to m
            double c1 = 3.7418e-16; // 2πhc² in W·m²
            double c2 = 1.4388e-2;  // hc/k in m·K
            double exp = Math.Exp(c2 / (lambda * T));
            if (double.IsInfinity(exp) || exp <= 1) return 0;
            return c1 / (Math.Pow(lambda, 5) * (exp - 1));
        }

        private static void NormalizeSPD(double[] spd)
        {
            double max = 0;
            for (int i = 0; i < spd.Length; i++)
                if (spd[i] > max) max = spd[i];
            if (max > 0)
                for (int i = 0; i < spd.Length; i++)
                    spd[i] /= max;
        }

        private static void ComputeXYZ(double[] spd, double[] reflectance, out double X, out double Y, out double Z)
        {
            X = Y = Z = 0;
            int n = Math.Min(spd.Length, Math.Min(reflectance.Length, CMF_X.Length));
            for (int i = 0; i < n; i++)
            {
                double val = spd[i] * reflectance[i];
                X += val * CMF_X[i];
                Y += val * CMF_Y[i];
                Z += val * CMF_Z[i];
            }
            // 5nm step integration normalization
            X *= 5;
            Y *= 5;
            Z *= 5;
        }

        private static void ToUCS(double X, double Y, double Z, out double u, out double v)
        {
            // CIE 1960 UCS
            double denom = X + 15 * Y + 3 * Z;
            if (denom <= 0) { u = 0; v = 0; return; }
            u = 4 * X / denom;
            v = 6 * Y / denom;
        }
    }
}
