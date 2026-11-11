using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.FileIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    internal static class LocalFrameFileService
    {
        private const int CopyBufferSize = 1024 * 1024;
        private static readonly object CapturePathSync = new();
        private static readonly HashSet<string> CapturePaths = new(StringComparer.OrdinalIgnoreCase);
        private static string? capturePathTimestamp;

        public static LocalFlowFrame Load(string filePath)
            => Load(filePath, null, null);

        internal static LocalFlowFrame Load(string filePath, float[]? exposureOverride, float? gainOverride)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("图像文件路径为空。", nameof(filePath));
            string fullPath = Path.GetFullPath(filePath);
            if (!CVFileReadCache.GetCachedLength(fullPath).HasValue && !File.Exists(fullPath)) throw new FileNotFoundException("图像文件不存在。", fullPath);

            using Stream source = CVFileReadCache.OpenRead(fullPath, populateCache: false);
            int dataOffset;
            CVCIEFile fileInfo;
            using (var stage = FlowNodeTiming.Measure("ReadImageHeader"))
            {
                dataOffset = CVFileUtil.ReadCIEFileHeader(source, out fileInfo);
                stage?.Complete();
            }
            using (fileInfo)
            {
                DateTime captureTime = source is FileStream disk ? File.GetLastWriteTime(disk.SafeFileHandle) : DateTime.Now;
                if (dataOffset > 0)
                {
                    return FlowNodeTiming.Run("ReadImageData", () =>
                    {
                        // Preserve cold CVRAW caching without caching non-ColorVision files during format detection.
                        if (source is not FileStream || !CVFileReadCache.IsEnabled || !string.Equals(Path.GetExtension(fullPath), ".cvraw", StringComparison.OrdinalIgnoreCase))
                            return LoadColorVisionFile(fullPath, source, fileInfo, dataOffset, captureTime, exposureOverride, gainOverride);

                        using Stream cached = CVFileReadCache.OpenRead(fullPath);
                        if (cached is FileStream)
                            return LoadColorVisionFile(fullPath, source, fileInfo, dataOffset, captureTime, exposureOverride, gainOverride);
                        // Another writer may have published a memory-only snapshot since the disk probe.
                        int cachedOffset = CVFileUtil.ReadCIEFileHeader(cached, out CVCIEFile cachedHeader);
                        using (cachedHeader)
                        {
                            if (cachedOffset <= 0) throw new InvalidDataException("图像文件头无效。");
                            return LoadColorVisionFile(fullPath, cached, cachedHeader, cachedOffset, captureTime, exposureOverride, gainOverride);
                        }
                    });
                }

                source.Position = 0;
                return FlowNodeTiming.Run("DecodeImage", () => LoadBitmap(fullPath, source, captureTime, exposureOverride, gainOverride));
            }
        }

        public static string CreateCapturePath(string basePath, string deviceCode, LocalFrameMetadata? metadata = null)
        {
            string root = string.IsNullOrWhiteSpace(basePath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ColorVision")
                : basePath;
            string safeDeviceCode = string.IsNullOrWhiteSpace(deviceCode) ? "CameraLocal" : deviceCode;
            string templateName = string.Empty;
            if (metadata?.IsMirrorReady == true && !string.IsNullOrWhiteSpace(metadata.CalibrationTemplate))
            {
                templateName = metadata.CalibrationTemplate.Trim();
                foreach (char invalid in Path.GetInvalidFileNameChars()) templateName = templateName.Replace(invalid, '_');
            }
            lock (CapturePathSync)
            {
                DateTime captureTime = DateTime.Now;
                string timestamp = captureTime.ToString("yyyyMMdd_HHmmss_fff");
                string directory = Path.Combine(root, safeDeviceCode, "Data", captureTime.ToString("yyyy-MM-dd"));
                // Reserve this millisecond's paths before callers save their database records and files.
                if (timestamp != capturePathTimestamp)
                {
                    CapturePaths.Clear();
                    capturePathTimestamp = timestamp;
                }
                for (int sequence = 1; ; sequence = checked(sequence + 1))
                {
                    string duplicateSuffix = sequence == 1 ? string.Empty : "_" + sequence;
                    int maximumTemplateLength = 255 - timestamp.Length - duplicateSuffix.Length - "_.cvraw".Length;
                    string templateSuffix = templateName.Length == 0 ? string.Empty : "_" + templateName[..Math.Min(templateName.Length, maximumTemplateLength)];
                    string path = Path.Combine(directory, timestamp + templateSuffix + duplicateSuffix + ".cvraw");
                    if (!File.Exists(path) && !CVFileReadCache.GetCachedLength(path).HasValue && CapturePaths.Add(Path.GetFullPath(path))) return path;
                }
            }
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
                if (lease.Metadata.Acquisition != null)
                    CameraFileMetadata.Save(rawPath, CameraFileMetadata.AcquisitionKind, lease.Metadata.Acquisition);
                frame.ColorCalibration?.Save(rawPath, canReplay: true);
                if (frame.ColorCalibration == null && frame.CalibrationProvenance != null)
                    CameraFileMetadata.Save(rawPath, CameraFileMetadata.ProvenanceKind, frame.CalibrationProvenance);
            }
            saveStage?.Complete();
        }

        private static LocalFlowFrame LoadColorVisionFile(
            string filePath,
            Stream stream,
            CVCIEFile fileInfo,
            int dataOffset,
            DateTime captureTime,
            float[]? exposureOverride,
            float? gainOverride)
        {
            using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);
            stream.Position = dataOffset;
            long dataLength = fileInfo.Version == 2 ? reader.ReadInt64() : reader.ReadInt32();
            if (dataLength <= 0 || dataLength > int.MaxValue || stream.Position + dataLength > stream.Length)
            {
                throw new InvalidDataException($"图像文件数据长度无效：{dataLength}");
            }

            bool isCie = string.Equals(Path.GetExtension(filePath), ".cvcie", StringComparison.OrdinalIgnoreCase);
            fileInfo.FileExtType = isCie ? CVType.CIE : CVType.Raw;
            var properties = isCie ? null : CVFileMetadata.ReadFromStream(stream);
            ColorCalibrationSnapshot? snapshot = properties == null ? null : ColorCalibrationSnapshot.Read(properties, fileInfo);
            var records = properties == null ? CameraFileMetadata.Read(stream) : CameraFileMetadata.Read(properties);
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
                Acquisition = records.Acquisition,
                PrimaryBufferKind = isCie ? LocalFrameBufferKind.CvCie : LocalFrameBufferKind.CvRaw
            };
            LocalFlowFrame frame = LocalFlowFrame.Allocate(metadata, isCie ? 0 : (int)dataLength, isCie ? (int)dataLength : 0);
            frame.CalibrationProvenance = records.Provenance;
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

        private static LocalFlowFrame LoadBitmap(string filePath, Stream stream, DateTime captureTime, float[]? exposureOverride, float? gainOverride)
        {
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

            int stride = checked(source.PixelWidth * (sourceBpp / 8) * channels);
            int length = checked(stride * source.PixelHeight);
            LocalFrameMetadata metadata = new()
            {
                Width = source.PixelWidth,
                Height = source.PixelHeight,
                SourceBpp = sourceBpp,
                Channels = channels,
                Gain = gainOverride ?? 1,
                Exposure = CloneExposure(exposureOverride),
                SourceFilePath = filePath,
                CaptureTime = captureTime,
                PrimaryBufferKind = LocalFrameBufferKind.CvRaw
            };
            LocalFlowFrame frame = LocalFlowFrame.Allocate(metadata, length, 0);
            try
            {
                using LocalFlowFrameLease lease = frame.Acquire();
                using var stage = FlowNodeTiming.Measure("DecodePixels");
                source.CopyPixels(Int32Rect.Empty, lease.RawPointer, length, stride);
                if (swapRgb48) SwapRgb48ToBgr48(lease.RawPointer, length);
                stage?.Complete();
                return frame;
            }
            catch
            {
                frame.Dispose();
                throw;
            }
        }

        private static unsafe void SwapRgb48ToBgr48(IntPtr pixels, int length)
        {
            Span<ushort> samples = new(pixels.ToPointer(), length / sizeof(ushort));
            for (int i = 0; i < samples.Length; i += 3)
            {
                (samples[i], samples[i + 2]) = (samples[i + 2], samples[i]);
            }
        }

        private static float[] CloneExposure(float[]? exposure)
            => exposure == null || exposure.Length == 0 ? Array.Empty<float>() : (float[])exposure.Clone();

        private static unsafe void CopyStreamToPointer(Stream stream, IntPtr destination, int length)
        {
            Span<byte> pixels = new(destination.ToPointer(), length);
            while (pixels.Length > CopyBufferSize)
            {
                stream.ReadExactly(pixels[..CopyBufferSize]);
                pixels = pixels[CopyBufferSize..];
            }
            stream.ReadExactly(pixels);
        }

        private static unsafe void WriteUnmanagedBuffer(Stream stream, IntPtr source, int length)
        {
            if (source == IntPtr.Zero || length <= 0)
                throw new ArgumentException("The source image buffer is empty.", nameof(source));

            // The writer consumes this span before returning, including cache-only saves.
            // The frame lease covers the entire save call.
            ReadOnlySpan<byte> pixels = new(source.ToPointer(), length);
            if (stream is FileStream)
            {
                while (pixels.Length > CopyBufferSize)
                {
                    stream.Write(pixels[..CopyBufferSize]);
                    pixels = pixels[CopyBufferSize..];
                }
            }
            stream.Write(pixels);
        }
    }
}
