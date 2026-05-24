using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;

namespace Conoscope.Analysis
{
    internal static class AnalysisResultCsvExporter
    {
        public static void ExportContrast(Window owner, ContrastComputationResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            string? filePath = SelectCsvFile(owner, "ContrastResult");
            if (filePath == null)
            {
                return;
            }

            try
            {
                WriteCsv(filePath, BuildContrastRows(result));
                MessageBox.Show(owner, Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgExportSuccess, filePath), Properties.Resources.TitleContrastResult, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgExportFailed, ex.Message), Properties.Resources.TitleContrastResult, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static void ExportColorGamut(Window owner, ColorGamutComputationResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            string? filePath = SelectCsvFile(owner, "ColorGamutResult");
            if (filePath == null)
            {
                return;
            }

            try
            {
                WriteCsv(filePath, BuildColorGamutRows(result));
                MessageBox.Show(owner, Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgExportSuccess, filePath), Properties.Resources.TitleColorGamutResult, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgExportFailed, ex.Message), Properties.Resources.TitleColorGamutResult, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string? SelectCsvFile(Window owner, string prefix)
        {
            SaveFileDialog dialog = new()
            {
                Filter = Properties.Resources.LabelSaveFilterCsv,
                FileName = $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
        }

        private static IEnumerable<IReadOnlyList<string>> BuildContrastRows(ContrastComputationResult result)
        {
            yield return new[]
            {
                "Index",
                "PointKey",
                "FocusPoint",
                "Azimuth(deg)",
                "Polar(deg)",
                "Radius(deg)",
                "WhiteFile",
                "WhiteX",
                "WhiteY(cd/m2)",
                "WhiteZ",
                "Whitex",
                "Whitey",
                "BlackFile",
                "BlackX",
                "BlackY(cd/m2)",
                "BlackZ",
                "Blackx",
                "Blacky",
                "ContrastRatio",
                "ContrastText"
            };

            foreach (ContrastPointResult item in result.Points)
            {
                yield return new[]
                {
                    item.Index.ToString(CultureInfo.InvariantCulture),
                    item.PointKey,
                    item.PointName,
                    FormatNullable(item.AzimuthDegrees),
                    FormatNullable(item.PolarDegrees),
                    FormatNullable(item.RadiusDegrees),
                    item.White.FileName,
                    FormatDouble(item.White.X),
                    FormatDouble(item.White.Y),
                    FormatDouble(item.White.Z),
                    FormatDouble(item.White.Chromaticity.x),
                    FormatDouble(item.White.Chromaticity.y),
                    item.Black.FileName,
                    FormatDouble(item.Black.X),
                    FormatDouble(item.Black.Y),
                    FormatDouble(item.Black.Z),
                    FormatDouble(item.Black.Chromaticity.x),
                    FormatDouble(item.Black.Chromaticity.y),
                    FormatDouble(item.Ratio),
                    item.RatioText
                };
            }
        }

        private static IEnumerable<IReadOnlyList<string>> BuildColorGamutRows(ColorGamutComputationResult result)
        {
            yield return new[]
            {
                "Standard",
                "Index",
                "PointKey",
                "FocusPoint",
                "Azimuth(deg)",
                "Polar(deg)",
                "Radius(deg)",
                "RedFile",
                "RedX",
                "RedY(cd/m2)",
                "RedZ",
                "Redx",
                "Redy",
                "GreenFile",
                "GreenX",
                "GreenY(cd/m2)",
                "GreenZ",
                "Greenx",
                "Greeny",
                "BlueFile",
                "BlueX",
                "BlueY(cd/m2)",
                "BlueZ",
                "Bluex",
                "Bluey",
                "SampleArea",
                "StandardArea",
                "Coverage(%)"
            };

            foreach (ColorGamutPointResult item in result.Points)
            {
                yield return new[]
                {
                    result.Standard.Name,
                    item.Index.ToString(CultureInfo.InvariantCulture),
                    item.PointKey,
                    item.PointName,
                    FormatNullable(item.AzimuthDegrees),
                    FormatNullable(item.PolarDegrees),
                    FormatNullable(item.RadiusDegrees),
                    item.Red.FileName,
                    FormatDouble(item.Red.X),
                    FormatDouble(item.Red.Y),
                    FormatDouble(item.Red.Z),
                    FormatDouble(item.Red.Chromaticity.x),
                    FormatDouble(item.Red.Chromaticity.y),
                    item.Green.FileName,
                    FormatDouble(item.Green.X),
                    FormatDouble(item.Green.Y),
                    FormatDouble(item.Green.Z),
                    FormatDouble(item.Green.Chromaticity.x),
                    FormatDouble(item.Green.Chromaticity.y),
                    item.Blue.FileName,
                    FormatDouble(item.Blue.X),
                    FormatDouble(item.Blue.Y),
                    FormatDouble(item.Blue.Z),
                    FormatDouble(item.Blue.Chromaticity.x),
                    FormatDouble(item.Blue.Chromaticity.y),
                    FormatDouble(item.SampleArea),
                    FormatDouble(item.StandardArea),
                    FormatDouble(item.CoveragePercent)
                };
            }
        }

        private static void WriteCsv(string filePath, IEnumerable<IReadOnlyList<string>> rows)
        {
            StringBuilder builder = new();
            foreach (IReadOnlyList<string> row in rows)
            {
                AppendCsvLine(builder, row);
            }

            File.WriteAllText(filePath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }

        private static void AppendCsvLine(StringBuilder builder, IReadOnlyList<string> values)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                builder.Append(EscapeCsvField(values[index]));
            }

            builder.AppendLine();
        }

        private static string EscapeCsvField(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\r') && !value.Contains('\n'))
            {
                return value;
            }

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        private static string FormatNullable(double? value)
        {
            return value.HasValue ? FormatDouble(value.Value) : string.Empty;
        }

        private static string FormatDouble(double value)
        {
            return value.ToString("G17", CultureInfo.InvariantCulture);
        }
    }
}
