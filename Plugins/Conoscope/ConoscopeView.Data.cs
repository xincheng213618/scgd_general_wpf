using ColorVision.FileIO;
using Conoscope.Core;
using System;
using System.IO;
using System.Windows;

namespace Conoscope
{
    public partial class ConoscopeView
    {
        public OpenCvSharp.Mat? XMat { get; set; }
        public OpenCvSharp.Mat? YMat { get; set; }
        public OpenCvSharp.Mat? ZMat { get; set; }

        string Filename = string.Empty;
        private string? captureExposureSummary;

        public bool HasCaptureExposureSummary => !string.IsNullOrWhiteSpace(captureExposureSummary);
        public string CaptureExposureSummary => captureExposureSummary ?? "未记录";

        public void OpenConoscope(string filename, string? exposureSummary = null)
        {
            try
            {
                Filename = filename;
                captureExposureSummary = string.IsNullOrWhiteSpace(exposureSummary) ? null : exposureSummary;
                HideCoordinateDragOverlay();
                DisposeCoordinateAxis();
                ImageView.Clear();
                LoadConoscopeData(filename);

                if (ConoscopeConfig.ApplyFilterOnOpen)
                {
                    ApplyPreprocessToCurrentMats();
                }

                RefreshDisplayedImage();
                SyncCieWindowFromCurrentPointer();
            }
            catch (Exception ex)
            {
                log.Error($"打开Conoscope图像失败: {ex.Message}", ex);
                MessageBox.Show($"打开图像失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadConoscopeData(string filename)
        {
            if (!CVFileUtil.IsCVCIEFile(filename))
            {
                throw new NotSupportedException("当前视图仅支持 CVCIE XYZ 图像文件");
            }

            ClearMatData();

            CVFileUtil.Read(filename, out CVCIEFile fileInfo);
            if (fileInfo.Channels < 3)
            {
                throw new NotSupportedException($"CVCIE 文件通道数不足: {fileInfo.Channels}");
            }

            int bytesPerPixel = fileInfo.Bpp / 8;
            int channelSize = fileInfo.Cols * fileInfo.Rows * bytesPerPixel;
            if (fileInfo.Data == null || fileInfo.Data.Length < channelSize * 3)
            {
                throw new InvalidDataException("CVCIE 文件数据长度不足，无法拆分 XYZ 通道");
            }

            OpenCvSharp.MatType singleChannelType = GetSingleChannelMatType(fileInfo.Bpp);
            XMat = CreateFloatChannelMat(fileInfo.Data, 0, channelSize, fileInfo.Rows, fileInfo.Cols, singleChannelType);
            YMat = CreateFloatChannelMat(fileInfo.Data, channelSize, channelSize, fileInfo.Rows, fileInfo.Cols, singleChannelType);
            ZMat = CreateFloatChannelMat(fileInfo.Data, channelSize * 2, channelSize, fileInfo.Rows, fileInfo.Cols, singleChannelType);
            ClampNonPositiveXyzValuesIfEnabled();

            log.Info($"已加载 CVCIE XYZ 数据: {fileInfo.Cols}x{fileInfo.Rows}, Bpp={fileInfo.Bpp}");
        }

        private static OpenCvSharp.MatType GetSingleChannelMatType(int bpp)
        {
            return bpp switch
            {
                8 => OpenCvSharp.MatType.CV_8UC1,
                16 => OpenCvSharp.MatType.CV_16UC1,
                32 => OpenCvSharp.MatType.CV_32FC1,
                64 => OpenCvSharp.MatType.CV_64FC1,
                _ => throw new NotSupportedException($"Bpp {bpp} not supported")
            };
        }

        private static OpenCvSharp.Mat CreateFloatChannelMat(byte[] source, int offset, int channelSize, int rows, int cols, OpenCvSharp.MatType sourceType)
        {
            byte[] channelData = new byte[channelSize];
            Buffer.BlockCopy(source, offset, channelData, 0, channelSize);

            using OpenCvSharp.Mat raw = OpenCvSharp.Mat.FromPixelData(rows, cols, sourceType, channelData);
            OpenCvSharp.Mat copied = raw.Clone();
            if (copied.Type() == OpenCvSharp.MatType.CV_32FC1)
            {
                return copied;
            }

            OpenCvSharp.Mat floatMat = new OpenCvSharp.Mat();
            copied.ConvertTo(floatMat, OpenCvSharp.MatType.CV_32FC1);
            copied.Dispose();
            return floatMat;
        }

        private void RestoreOriginalMats()
        {
            if (string.IsNullOrWhiteSpace(Filename))
            {
                return;
            }

            LoadConoscopeData(Filename);
        }

        private void ClearMatData()
        {
            XMat?.Dispose();
            XMat = null;
            YMat?.Dispose();
            YMat = null;
            ZMat?.Dispose();
            ZMat = null;
        }
    }
}
