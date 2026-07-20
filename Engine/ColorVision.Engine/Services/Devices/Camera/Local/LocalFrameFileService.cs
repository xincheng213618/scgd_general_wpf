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
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("图像文件路径为空。", nameof(filePath));
            string fullPath = Path.GetFullPath(filePath);
            if (!File.Exists(fullPath)) throw new FileNotFoundException("图像文件不存在。", fullPath);

            int dataOffset = CVFileUtil.ReadCIEFileHeader(fullPath, out CVCIEFile fileInfo);
            if (dataOffset > 0)
            {
                return LoadColorVisionFile(fullPath, fileInfo, dataOffset);
            }

            return LoadBitmap(fullPath);
        }

        public static void SaveCapture(LocalFlowFrame frame, string basePath, string deviceCode)
        {
            using LocalFlowFrameLease lease = frame.Acquire();
            string root = string.IsNullOrWhiteSpace(basePath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ColorVision")
                : basePath;
            string safeDeviceCode = string.IsNullOrWhiteSpace(deviceCode) ? "CameraLocal" : deviceCode;
            string directory = Path.Combine(root, safeDeviceCode, "Data", DateTime.Now.ToString("yyyy-MM-dd"));
            Directory.CreateDirectory(directory);
            string stem = $"Local_{DateTime.Now:yyyyMMdd_HHmmss_fff}";

            if (lease.HasRaw)
            {
                string rawPath = Path.Combine(directory, stem + ".cvraw");
                CVCIEFile rawFile = BuildFileInfo(lease, CVType.Raw, lease.CopyRawToArray(), string.Empty, lease.Metadata.SourceBpp);
                if (!CVFileUtil.WriteCVRaw(rawPath, rawFile)) throw new IOException($"保存 CVRAW 失败：{rawPath}");
                frame.CvRawFilePath = rawPath;
            }

            if (lease.HasCie)
            {
                string ciePath = Path.Combine(directory, stem + ".cvcie");
                CVCIEFile cieFile = BuildFileInfo(lease, CVType.CIE, lease.CopyCieToArray(), Path.GetFileName(frame.CvRawFilePath), lease.Metadata.CieBpp);
                if (!CVFileUtil.WriteCVCIE(ciePath, cieFile)) throw new IOException($"保存 CVCIE 失败：{ciePath}");
                frame.CvCieFilePath = ciePath;
            }
        }

        private static LocalFlowFrame LoadColorVisionFile(string filePath, CVCIEFile fileInfo, int dataOffset)
        {
            using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using BinaryReader reader = new(stream);
            stream.Position = dataOffset;
            long dataLength = fileInfo.Version == 2 ? reader.ReadInt64() : reader.ReadInt32();
            if (dataLength <= 0 || dataLength > int.MaxValue || stream.Position + dataLength > stream.Length)
            {
                throw new InvalidDataException($"图像文件数据长度无效：{dataLength}");
            }

            bool isCie = string.Equals(Path.GetExtension(filePath), ".cvcie", StringComparison.OrdinalIgnoreCase);
            LocalFrameMetadata metadata = new()
            {
                Width = fileInfo.Cols,
                Height = fileInfo.Rows,
                SourceBpp = isCie ? 0 : fileInfo.Bpp,
                CieBpp = isCie ? fileInfo.Bpp : 32,
                Channels = fileInfo.Channels,
                Gain = fileInfo.Gain,
                Exposure = fileInfo.Exp ?? Array.Empty<float>(),
                SourceFilePath = filePath,
                CaptureTime = File.GetLastWriteTime(filePath),
                PrimaryBufferKind = isCie ? LocalFrameBufferKind.CvCie : LocalFrameBufferKind.CvRaw
            };
            LocalFlowFrame frame = LocalFlowFrame.Allocate(metadata, isCie ? 0 : (int)dataLength, isCie ? (int)dataLength : 0);
            try
            {
                using LocalFlowFrameLease lease = frame.Acquire();
                CopyStreamToPointer(stream, isCie ? lease.CiePointer : lease.RawPointer, (int)dataLength);
                if (isCie) frame.CvCieFilePath = filePath;
                else frame.CvRawFilePath = filePath;
                return frame;
            }
            catch
            {
                frame.Dispose();
                throw;
            }
        }

        private static LocalFlowFrame LoadBitmap(string filePath)
        {
            using FileStream stream = File.OpenRead(filePath);
            BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            BitmapSource source = decoder.Frames[0];
            if (source.Format != PixelFormats.Bgra32)
            {
                source = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            }

            int stride = checked(source.PixelWidth * 4);
            int length = checked(stride * source.PixelHeight);
            byte[] pixels = new byte[length];
            source.CopyPixels(pixels, stride, 0);
            LocalFrameMetadata metadata = new()
            {
                Width = source.PixelWidth,
                Height = source.PixelHeight,
                SourceBpp = 8,
                Channels = 4,
                SourceFilePath = filePath,
                CaptureTime = File.GetLastWriteTime(filePath),
                PrimaryBufferKind = LocalFrameBufferKind.Source
            };
            LocalFlowFrame frame = LocalFlowFrame.Allocate(metadata, length, 0);
            try
            {
                using LocalFlowFrameLease lease = frame.Acquire();
                Marshal.Copy(pixels, 0, lease.RawPointer, pixels.Length);
                return frame;
            }
            catch
            {
                frame.Dispose();
                throw;
            }
        }

        private static CVCIEFile BuildFileInfo(LocalFlowFrameLease lease, CVType type, byte[] data, string sourceFileName, int bpp)
        {
            return new CVCIEFile
            {
                Version = 1,
                FileExtType = type,
                Rows = lease.Metadata.Height,
                Cols = lease.Metadata.Width,
                Bpp = bpp,
                Channels = lease.Metadata.Channels,
                Gain = lease.Metadata.Gain,
                Exp = lease.Metadata.Exposure,
                SrcFileName = sourceFileName,
                Data = data
            };
        }

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
    }
}
