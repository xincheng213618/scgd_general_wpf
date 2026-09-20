using OpenCvSharp;
using System;
using System.Collections.Generic;

namespace Conoscope.Core
{
    /// <summary>
    /// Retains reference-counted headers over published, read-only channel buffers.
    /// Preprocessing and reference-store updates replace buffers, so exports survive
    /// document disposal without cloning several hundred MiB of source pixels.
    /// Capture on the document's UI thread and dispose only after the worker ends.
    /// </summary>
    internal sealed class ConoscopeExportSource : IDisposable
    {
        private readonly List<Mat> retained = new();
        public ConoscopeExportContext Context { get; }

        public ConoscopeExportSource(string model, System.Windows.Point center, double maxAngle, double pixelsPerDegree,
            Mat? x, Mat y, Mat? z, ColorDifferenceReferenceMode differenceMode, ConoscopeUvReference? pointReference,
            Mat? referenceU, Mat? referenceV, ContrastReferenceKind contrastKind, Mat? referenceY)
        {
            try
            {
                Mat? heldX = Retain(x);
                Mat heldY = Retain(y)!;
                Mat? heldZ = Retain(z);
                Mat? heldU = differenceMode == ColorDifferenceReferenceMode.ReferenceImage ? Retain(referenceU) : null;
                Mat? heldV = differenceMode == ColorDifferenceReferenceMode.ReferenceImage ? Retain(referenceV) : null;
                Mat? heldReferenceY = Retain(referenceY);
                int width = heldY.Width;
                int height = heldY.Height;
                Context = new()
                {
                    ModelName = model, Center = center, MaxAngle = maxAngle, PixelsPerDegree = pixelsPerDegree,
                    ImageWidth = width, ImageHeight = height,
                    ReadXyz = ReadXyz,
                    ReadColorDifference = (ix, iy) =>
                    {
                        ConoscopeXyzValue xyz = ReadXyz(ix, iy);
                        if (differenceMode == ColorDifferenceReferenceMode.ReferenceImage)
                        {
                            if (heldU == null || heldV == null) return 0;
                            int sx = Math.Clamp(ix, 0, heldU.Width - 1);
                            int sy = Math.Clamp(iy, 0, heldU.Height - 1);
                            return ConoscopeColorimetry.CalculateColorDifference(xyz.X, xyz.Y, xyz.Z, heldU.At<float>(sy, sx), heldV.At<float>(sy, sx));
                        }
                        return pointReference is { } reference
                            ? ConoscopeColorimetry.CalculateColorDifference(xyz.X, xyz.Y, xyz.Z, reference.U, reference.V) : 0;
                    },
                    ReadContrast = (ix, iy) => heldReferenceY == null || heldReferenceY.Width != width || heldReferenceY.Height != height
                        ? double.NaN : ConoscopeColorimetry.CalculateContrast(heldY.At<float>(iy, ix), heldReferenceY.At<float>(iy, ix), contrastKind)
                };

                ConoscopeXyzValue ReadXyz(int ix, int iy) => new(heldX?.At<float>(iy, ix) ?? 0, heldY.At<float>(iy, ix), heldZ?.At<float>(iy, ix) ?? 0);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private Mat? Retain(Mat? source)
        {
            if (source == null) return null;
            Mat header = source.SubMat(0, source.Rows, 0, source.Cols);
            retained.Add(header);
            return header;
        }

        public void Dispose()
        {
            foreach (Mat mat in retained) mat.Dispose();
            retained.Clear();
        }
    }
}
