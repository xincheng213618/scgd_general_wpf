using ColorVision.ImageEditor.Cie;
using Conoscope.Core;
using System;
using System.Collections.Generic;
using System.IO;

namespace Conoscope.Analysis
{
    public sealed record ImageMeasurement(
        string FilePath,
        double X,
        double Y,
        double Z,
        ConoscopeChromaticity Chromaticity)
    {
        public string FileName => Path.GetFileName(FilePath);
        public double Luminance => Y;
    }

    public readonly record struct ChromaticityPoint(double X, double Y);

    public sealed record ColorGamutStandard(string Name, ChromaticityPoint Red, ChromaticityPoint Green, ChromaticityPoint Blue)
    {
        public override string ToString() => Name;
    }

    public static class ColorGamutStandards
    {
        public static IReadOnlyList<ColorGamutStandard> All { get; } = new[]
        {
            FromGamut(CieGamuts.SRgb),
            FromGamut(CieGamuts.Rec709),
            FromGamut(CieGamuts.AdobeRgb),
            FromGamut(CieGamuts.DisplayP3),
            FromGamut(CieGamuts.DciP3),
            FromGamut(CieGamuts.Rec2020),
            FromGamut(CieGamuts.Ntsc1953),
            FromGamut(CieGamuts.EbuPal),
            FromGamut(CieGamuts.SmpteC),
            FromGamut(CieGamuts.ProPhotoRgb),
            FromGamut(CieGamuts.AcesCg)
        };

        private static ColorGamutStandard FromGamut(CieGamut gamut)
        {
            if (gamut.Vertices.Count < 3)
            {
                throw new InvalidOperationException(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.GamutNeedsAtLeastThreeVertices, gamut.Name));
            }

            return new ColorGamutStandard(
                gamut.Name,
                ToPoint(gamut.Vertices[0]),
                ToPoint(gamut.Vertices[1]),
                ToPoint(gamut.Vertices[2]));
        }

        private static ChromaticityPoint ToPoint(CieChromaticity chromaticity)
        {
            return new ChromaticityPoint(chromaticity.X, chromaticity.Y);
        }
    }
}
