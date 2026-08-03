using FlowEngineLib.Algorithm;
using OpenCvSharp;
using System;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    /// <summary>
    /// Applies the workflow image orientation directly to the primary unmanaged
    /// frame buffer. Spatial calibration must run before this transform.
    /// </summary>
    internal static class LocalFrameMirrorService
    {
        public static void ApplyPending(LocalFlowFrame frame)
        {
            ArgumentNullException.ThrowIfNull(frame);
            frame.ApplyPendingFlip(FlipPrimaryInPlace);
        }

        public static void FlipPrimaryInPlace(
            LocalFlowFrameLease lease,
            LocalFrameBufferKind bufferKind,
            CVImageFlipMode flipMode)
        {
            ArgumentNullException.ThrowIfNull(lease);
            ValidateFlipMode(flipMode);
            if (flipMode == CVImageFlipMode.None) return;

            if (bufferKind == LocalFrameBufferKind.CvCie)
            {
                if (!lease.HasCie) throw new InvalidOperationException("The primary CIE buffer is missing.");
                int cieChannels = GetCieChannelCount(
                    lease.CieLength,
                    lease.Metadata.Width,
                    lease.Metadata.Height);
                FlipPlanarCie(
                    lease.CiePointer,
                    lease.CieLength,
                    lease.Metadata.Width,
                    lease.Metadata.Height,
                    cieChannels,
                    flipMode);
                return;
            }

            if (!lease.HasRaw) throw new InvalidOperationException("The primary RAW buffer is missing.");
            FlipRaw(
                lease.RawPointer,
                lease.RawLength,
                lease.Metadata.Width,
                lease.Metadata.Height,
                lease.Metadata.SourceBpp,
                lease.Metadata.Channels,
                flipMode);
        }

        private static void FlipRaw(
            IntPtr pointer,
            int byteLength,
            int width,
            int height,
            int bitsPerChannel,
            int channels,
            CVImageFlipMode flipMode)
        {
            if (pointer == IntPtr.Zero) throw new ArgumentException("RAW pointer is null.", nameof(pointer));
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Image dimensions must be positive.");
            MatType type = (bitsPerChannel, channels) switch
            {
                (8, 1) => MatType.CV_8UC1,
                (8, 3) => MatType.CV_8UC3,
                (16, 1) => MatType.CV_16UC1,
                (16, 3) => MatType.CV_16UC3,
                _ => throw new NotSupportedException($"Mirror supports 8/16-bit, 1/3-channel RAW; actual layout is {bitsPerChannel}-bit, {channels} channels."),
            };
            int requiredLength = checked(width * height * channels * (bitsPerChannel / 8));
            if (byteLength < requiredLength)
            {
                throw new ArgumentException($"RAW buffer is too small: required {requiredLength}, actual {byteLength}.", nameof(byteLength));
            }

            using Mat image = Mat.FromPixelData(height, width, type, pointer);
            Cv2.Flip(image, image, ToOpenCvFlipMode(flipMode));
        }

        private static void FlipPlanarCie(
            IntPtr pointer,
            int byteLength,
            int width,
            int height,
            int channels,
            CVImageFlipMode flipMode)
        {
            if (pointer == IntPtr.Zero) throw new ArgumentException("CIE pointer is null.", nameof(pointer));
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Image dimensions must be positive.");
            if (channels is not (1 or 3)) throw new NotSupportedException($"Mirror supports one- or three-plane CIE; actual planes: {channels}.");
            int planeBytes = checked(width * height * sizeof(float));
            int requiredLength = checked(planeBytes * channels);
            if (byteLength < requiredLength)
            {
                throw new ArgumentException($"CIE buffer is too small: required {requiredLength}, actual {byteLength}.", nameof(byteLength));
            }

            OpenCvSharp.FlipMode openCvFlipMode = ToOpenCvFlipMode(flipMode);
            for (int channel = 0; channel < channels; channel++)
            {
                using Mat plane = Mat.FromPixelData(height, width, MatType.CV_32FC1, IntPtr.Add(pointer, checked(channel * planeBytes)));
                Cv2.Flip(plane, plane, openCvFlipMode);
            }
        }

        private static int GetCieChannelCount(int byteLength, int width, int height)
        {
            int planeBytes = checked(width * height * sizeof(float));
            if (planeBytes == 0 || byteLength % planeBytes != 0)
            {
                throw new ArgumentException($"CIE buffer length {byteLength} does not match {width}x{height} float planes.", nameof(byteLength));
            }
            int channels = byteLength / planeBytes;
            return channels is 1 or 3
                ? channels
                : throw new NotSupportedException($"Mirror supports one- or three-plane CIE; actual planes: {channels}.");
        }

        private static OpenCvSharp.FlipMode ToOpenCvFlipMode(CVImageFlipMode flipMode)
            => flipMode switch
            {
                CVImageFlipMode.X => OpenCvSharp.FlipMode.X,
                CVImageFlipMode.Y => OpenCvSharp.FlipMode.Y,
                CVImageFlipMode.XY => OpenCvSharp.FlipMode.XY,
                _ => throw new ArgumentOutOfRangeException(nameof(flipMode), flipMode, "Unsupported image mirror mode."),
            };

        public static void ValidateFlipMode(CVImageFlipMode flipMode)
        {
            if (flipMode is not (CVImageFlipMode.None or CVImageFlipMode.X or CVImageFlipMode.Y or CVImageFlipMode.XY))
            {
                throw new ArgumentOutOfRangeException(nameof(flipMode), flipMode, "Unsupported image mirror mode.");
            }
        }
    }
}
