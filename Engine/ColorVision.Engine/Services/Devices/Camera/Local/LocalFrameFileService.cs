using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.FileIO;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    internal static class LocalFrameFileService
    {
        private const int CopyBufferSize = 1024 * 1024;

        public static LocalFlowFrame Load(string filePath)
            => Load(filePath, null, null);

        internal static LocalFlowFrame Load(string filePath, float[]? exposureOverride, float? gainOverride)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("图像文件路径为空。", nameof(filePath));
            string fullPath = Path.GetFullPath(filePath);
            if (!CVFileReadCache.GetCachedLength(fullPath).HasValue && !File.Exists(fullPath)) throw new FileNotFoundException("图像文件不存在。", fullPath);

            int dataOffset;
            CVCIEFile fileInfo;
            using (var stage = FlowNodeTiming.Measure("ReadImageHeader"))
            {
                dataOffset = CVFileUtil.ReadCIEFileHeader(fullPath, out fileInfo);
                stage?.Complete();
            }
            if (dataOffset > 0)
            {
                return FlowNodeTiming.Run("ReadImageData", () => LoadColorVisionFile(fullPath, fileInfo, dataOffset, exposureOverride, gainOverride));
            }

            return FlowNodeTiming.Run("DecodeImage", () => LoadBitmap(fullPath, exposureOverride, gainOverride));
        }

        public static string CreateCapturePath(string basePath, string deviceCode)
        {
            string root = string.IsNullOrWhiteSpace(basePath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ColorVision")
                : basePath;
            string safeDeviceCode = string.IsNullOrWhiteSpace(deviceCode) ? "CameraLocal" : deviceCode;
            string directory = Path.Combine(root, safeDeviceCode, "Data", DateTime.Now.ToString("yyyy-MM-dd"));
            return Path.Combine(directory, $"Local_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.cvraw");
        }

        public static void SaveCapture(LocalFlowFrame frame, string rawPath, CVFileSaveMode saveMode = CVFileSaveMode.Synchronous)
        {
            using var saveStage = FlowNodeTiming.Measure("SaveImage");
            using LocalFlowFrameLease lease = frame.Acquire();
            if (lease.Metadata.IsMirrorReady && !lease.IsFlipApplied)
                throw new InvalidOperationException("The primary frame cannot be saved before its mirror operation completes.");
            if (saveMode != CVFileSaveMode.MemoryOnly) Directory.CreateDirectory(Path.GetDirectoryName(rawPath)!);
            if (lease.HasRaw)
            {
                if (lease.IsBufferFlipFailed(LocalFrameBufferKind.CvRaw))
                {
                    throw new InvalidOperationException("The RAW mirror operation failed; the frame cannot be saved safely.");
                }
                // Copy the runtime orientation exactly. Deferred/non-primary RAW stays
                // canonical; materializing its pending mirror only in the file would
                // lose orientation metadata and break spatial calibration after reload.
                using CVCIEFile rawFile = new()
                {
                    Version = 1,
                    FileExtType = CVType.Raw,
                    Rows = lease.Metadata.Height,
                    Cols = lease.Metadata.Width,
                    Bpp = lease.Metadata.SourceBpp,
                    Channels = lease.Metadata.Channels,
                    Gain = lease.Metadata.Gain,
                    Exp = lease.Metadata.Exposure,
                    SrcFileName = string.Empty,
                    Data = Array.Empty<byte>()
                };
                FlowNodeTiming.Run("WriteRawFile", () =>
                {
                    if (!CVFileUtil.WriteCVRaw(
                        rawPath,
                        rawFile,
                        lease.RawLength,
                        stream => WriteUnmanagedBuffer(stream, lease.RawPointer, lease.RawLength), saveMode))
                    {
                        throw new IOException($"保存 CVRAW 失败：{rawPath}");
                    }
                });
                frame.CvRawFilePath = rawPath;
                frame.ColorCalibration?.Save(rawPath, canReplay: true);
            }
            saveStage?.Complete();
        }

        private static LocalFlowFrame LoadColorVisionFile(
            string filePath,
            CVCIEFile fileInfo,
            int dataOffset,
            float[]? exposureOverride,
            float? gainOverride)
        {
            DateTime captureTime = CVFileReadCache.GetCachedLength(filePath).HasValue ? DateTime.Now : File.GetLastWriteTime(filePath);
            using Stream stream = CVFileReadCache.OpenRead(filePath);
            using BinaryReader reader = new(stream);
            stream.Position = dataOffset;
            long dataLength = fileInfo.Version == 2 ? reader.ReadInt64() : reader.ReadInt32();
            if (dataLength <= 0 || dataLength > int.MaxValue || stream.Position + dataLength > stream.Length)
            {
                throw new InvalidDataException($"图像文件数据长度无效：{dataLength}");
            }

            bool isCie = string.Equals(Path.GetExtension(filePath), ".cvcie", StringComparison.OrdinalIgnoreCase);
            fileInfo.FileExtType = isCie ? CVType.CIE : CVType.Raw;
            ColorCalibrationSnapshot? snapshot = isCie ? null : ColorCalibrationSnapshot.Read(filePath, fileInfo);
            LocalFrameMetadata metadata = new()
            {
                Width = fileInfo.Cols,
                Height = fileInfo.Rows,
                SourceBpp = isCie ? 0 : fileInfo.Bpp,
                CieBpp = isCie ? fileInfo.Bpp : 32,
                Channels = fileInfo.Channels,
                Gain = gainOverride ?? fileInfo.Gain,
                Exposure = CloneExposure(exposureOverride ?? fileInfo.Exp),
                SourceFilePath = filePath,
                CalibrationTemplate = snapshot?.Template ?? string.Empty,
                CaptureTime = captureTime,
                PrimaryBufferKind = isCie ? LocalFrameBufferKind.CvCie : LocalFrameBufferKind.CvRaw
            };
            LocalFlowFrame frame = LocalFlowFrame.Allocate(metadata, isCie ? 0 : (int)dataLength, isCie ? (int)dataLength : 0);
            try
            {
                using LocalFlowFrameLease lease = frame.Acquire();
                FlowNodeTiming.Run("ReadImageBuffer", () => CopyStreamToPointer(stream, isCie ? lease.CiePointer : lease.RawPointer, (int)dataLength));
                if (isCie) frame.CvCieFilePath = filePath;
                else
                {
                    frame.CvRawFilePath = filePath;
                    frame.ColorCalibration = snapshot;
                }
                return frame;
            }
            catch
            {
                frame.Dispose();
                throw;
            }
        }

        private static LocalFlowFrame LoadBitmap(string filePath, float[]? exposureOverride, float? gainOverride)
        {
            using FileStream stream = File.OpenRead(filePath);
            BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            BitmapSource source = decoder.Frames[0];

            int sourceBpp;
            int channels;
            bool swapRgb48 = false;
            if (source.Format == PixelFormats.Gray8)
            {
                sourceBpp = 8;
                channels = 1;
            }
            else if (source.Format == PixelFormats.Gray16)
            {
                sourceBpp = 16;
                channels = 1;
            }
            else if (source.Format == PixelFormats.Rgb48)
            {
                sourceBpp = 16;
                channels = 3;
                swapRgb48 = true;
            }
            else if (source.Format == PixelFormats.Bgr24)
            {
                sourceBpp = 8;
                channels = 3;
            }
            else if (source.Format.BitsPerPixel is 24 or 32)
            {
                source = new FormatConvertedBitmap(source, PixelFormats.Bgr24, null, 0);
                sourceBpp = 8;
                channels = 3;
            }
            else
            {
                throw new NotSupportedException($"本地校正仅支持 Gray8、Gray16、24/32 位彩色或 Rgb48 图像，当前格式：{source.Format}。");
            }

            int stride = checked(source.PixelWidth * sourceBpp / 8 * channels);
            int length = checked(stride * source.PixelHeight);
            byte[] pixels = new byte[length];
            FlowNodeTiming.Run("DecodePixels", () => source.CopyPixels(pixels, stride, 0));
            if (swapRgb48)
            {
                for (int offset = 0; offset < pixels.Length; offset += 6)
                {
                    (pixels[offset], pixels[offset + 4]) = (pixels[offset + 4], pixels[offset]);
                    (pixels[offset + 1], pixels[offset + 5]) = (pixels[offset + 5], pixels[offset + 1]);
                }
            }

            LocalFrameMetadata metadata = new()
            {
                Width = source.PixelWidth,
                Height = source.PixelHeight,
                SourceBpp = sourceBpp,
                Channels = channels,
                Gain = gainOverride ?? 1,
                Exposure = CloneExposure(exposureOverride),
                SourceFilePath = filePath,
                CaptureTime = File.GetLastWriteTime(filePath),
                PrimaryBufferKind = LocalFrameBufferKind.CvRaw
            };
            LocalFlowFrame frame = LocalFlowFrame.Allocate(metadata, length, 0);
            try
            {
                using LocalFlowFrameLease lease = frame.Acquire();
                FlowNodeTiming.Run("CopyImageBuffer", () => Marshal.Copy(pixels, 0, lease.RawPointer, pixels.Length));
                return frame;
            }
            catch
            {
                frame.Dispose();
                throw;
            }
        }

        private static float[] CloneExposure(float[]? exposure)
            => exposure == null || exposure.Length == 0 ? Array.Empty<float>() : (float[])exposure.Clone();

        private static void CopyStreamToPointer(Stream stream, IntPtr destination, int length)
        {
            byte[] buffer = new byte[Math.Min(CopyBufferSize, length)];
            int offset = 0;
            while (offset < length)
            {
                int requested = Math.Min(buffer.Length, length - offset);
                int read = stream.Read(buffer, 0, requested);
                if (read <= 0) throw new EndOfStreamException("读取图像数据时提前到达文件尾。");
                Marshal.Copy(buffer, 0, IntPtr.Add(destination, offset), read);
                offset += read;
            }
        }

        private static unsafe void WriteUnmanagedBuffer(Stream stream, IntPtr source, int length)
        {
            if (source == IntPtr.Zero || length <= 0)
                throw new ArgumentException("The source image buffer is empty.", nameof(source));

            // The writer consumes this span before returning, including cache-only saves.
            // The frame lease covers the entire save call.
            stream.Write(new ReadOnlySpan<byte>(source.ToPointer(), length));
        }
    }
}
