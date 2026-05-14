using OpenCvSharp;
using System;

namespace Conoscope.Domain.Models
{
    public sealed class ConoscopeImageData : IDisposable
    {
        private Mat? x;
        private Mat? y;
        private Mat? z;

        public ConoscopeImageData(Mat x, Mat y, Mat z, int bitsPerPixel, string? exposureSummary = null)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            Width = x.Width;
            Height = x.Height;
            BitsPerPixel = bitsPerPixel;
            ExposureSummary = exposureSummary;
        }

        public int BitsPerPixel { get; }

        public string? ExposureSummary { get; }

        public int Width { get; }

        public int Height { get; }

        public Mat X => x ?? throw new ObjectDisposedException(nameof(ConoscopeImageData));

        public Mat Y => y ?? throw new ObjectDisposedException(nameof(ConoscopeImageData));

        public Mat Z => z ?? throw new ObjectDisposedException(nameof(ConoscopeImageData));

        public (Mat X, Mat Y, Mat Z) Detach()
        {
            Mat detachedX = X;
            Mat detachedY = Y;
            Mat detachedZ = Z;
            x = null;
            y = null;
            z = null;
            return (detachedX, detachedY, detachedZ);
        }

        public void Dispose()
        {
            x?.Dispose();
            x = null;
            y?.Dispose();
            y = null;
            z?.Dispose();
            z = null;
            GC.SuppressFinalize(this);
        }
    }
}