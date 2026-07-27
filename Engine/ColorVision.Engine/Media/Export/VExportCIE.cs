using ColorVision.Common.MVVM;
using ColorVision.FileIO;
using ColorVision.Solution.Mru;
using log4net;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing.Imaging;
using System.IO;

namespace ColorVision.Engine.Media
{
    public class VExportCIE : ViewModelBase
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(VExportCIE));

        public const int DefaultTiffCompression = 5;
        public const int DefaultPngCompressionLevel = 1;
        public const int DefaultJpegQuality = 95;

        public IReadOnlyDictionary<string, int> TiffCompressionOptions { get; } = new Dictionary<string, int>
        {
            ["LZW"] = DefaultTiffCompression,
            ["Deflate"] = 8,
            ["PackBits"] = 32773,
            ["None"] = 1,
        };

        public static void SaveTo(VExportCIE export, Mat src, string fileName)
        {
            ArgumentNullException.ThrowIfNull(export);
            ArgumentNullException.ThrowIfNull(src);
            if (src.Empty())
            {
                throw new InvalidOperationException("Cannot export an empty image.");
            }

            fileName = Path.ChangeExtension(fileName.TrimEnd('.'), GetFileExtension(export.ExportImageFormat));
            export.CoverFilePath = fileName;
            using Mat image = src.Clone();
            if (IsFormat(export.ExportImageFormat, ImageFormat.Tiff))
            {
                image.SaveImage(fileName, new ImageEncodingParam(ImwriteFlags.TiffCompression, export.TiffCompression));
            }
            else if (IsFormat(export.ExportImageFormat, ImageFormat.Bmp))
            {
                using Mat bmpImage = CreateBmpCompatibleMat(image);
                bmpImage.SaveImage(fileName);
            }
            else if (IsFormat(export.ExportImageFormat, ImageFormat.Png))
            {
                image.SaveImage(fileName, new ImageEncodingParam(ImwriteFlags.PngCompression, export.PngCompressionLevel));
            }
            else if (IsFormat(export.ExportImageFormat, ImageFormat.Jpeg))
            {
                image.SaveImage(fileName, new ImageEncodingParam(ImwriteFlags.JpegQuality, export.JpegQuality));
            }
            else
            {
                throw new NotSupportedException($"Unsupported export image format: {export.ExportImageFormat}.");
            }
        }

        private static bool IsFormat(ImageFormat value, ImageFormat expected)
        {
            return value.Guid == expected.Guid;
        }

        private static string GetFileExtension(ImageFormat imageFormat)
        {
            if (IsFormat(imageFormat, ImageFormat.Tiff))
                return ".tiff";
            if (IsFormat(imageFormat, ImageFormat.Bmp))
                return ".bmp";
            if (IsFormat(imageFormat, ImageFormat.Png))
                return ".png";
            if (IsFormat(imageFormat, ImageFormat.Jpeg))
                return ".jpg";
            throw new NotSupportedException($"Unsupported export image format: {imageFormat}.");
        }

        public static Mat CreateBmpCompatibleMat(Mat src)
        {
            ArgumentNullException.ThrowIfNull(src);
            if (src.Empty())
            {
                throw new InvalidOperationException("Cannot export an empty image.");
            }
            if (src.Depth() == MatType.CV_8U)
            {
                return src.Clone();
            }

            Mat dst = new();
            if (src.Depth() == MatType.CV_16U)
            {
                src.ConvertTo(dst, MatType.CV_8U, 1.0 / 256.0);
                return dst;
            }

            using Mat normalized = new();
            Cv2.Normalize(src, normalized, 0, 255, NormTypes.MinMax);
            normalized.ConvertTo(dst, MatType.CV_8U);
            return dst;
        }

        private static Mat CreateMatFromCVCIEFile(CVCIEFile fileInfo)
        {
            ValidateImageBuffer(fileInfo, fileInfo.Channels, fileInfo.Data);
            return Mat.FromPixelData(fileInfo.Rows, fileInfo.Cols, MatType.MakeType(fileInfo.Depth, fileInfo.Channels), fileInfo.Data);
        }

        private static Mat CreateSingleChannelMat(CVCIEFile fileInfo, byte[] data)
        {
            ValidateImageBuffer(fileInfo, 1, data);
            return Mat.FromPixelData(fileInfo.Rows, fileInfo.Cols, MatType.MakeType(fileInfo.Depth, 1), data);
        }

        private static byte[] CopyChannelData(CVCIEFile fileInfo, int channelIndex)
        {
            int len = GetSingleChannelByteCount(fileInfo);
            int offset = checked(channelIndex * len);
            if (fileInfo.Data == null || fileInfo.Data.Length < offset + len)
            {
                throw new InvalidDataException($"CIE data length is insufficient for channel {channelIndex}.");
            }

            byte[] data = new byte[len];
            Buffer.BlockCopy(fileInfo.Data, offset, data, 0, len);
            return data;
        }

        private static int GetSingleChannelByteCount(CVCIEFile fileInfo)
        {
            int bytesPerPixel = GetBytesPerPixel(fileInfo.Bpp);
            long count = checked((long)fileInfo.Cols * fileInfo.Rows * bytesPerPixel);
            if (count > int.MaxValue)
            {
                throw new InvalidDataException("Image channel data is too large.");
            }
            return (int)count;
        }

        private static void ValidateImageBuffer(CVCIEFile fileInfo, int channels, byte[] data)
        {
            if (fileInfo.Rows <= 0 || fileInfo.Cols <= 0)
            {
                throw new InvalidDataException($"Invalid image size: {fileInfo.Rows}x{fileInfo.Cols}.");
            }
            if (channels <= 0)
            {
                throw new InvalidDataException($"Invalid channel count: {channels}.");
            }

            int bytesPerPixel = GetBytesPerPixel(fileInfo.Bpp);
            long expectedLength = checked((long)fileInfo.Rows * fileInfo.Cols * channels * bytesPerPixel);
            if (data == null || data.LongLength < expectedLength)
            {
                throw new InvalidDataException($"Image data length is insufficient. Expected at least {expectedLength}, actual {data?.LongLength ?? 0}.");
            }
        }

        private static int GetBytesPerPixel(int bpp)
        {
            return bpp switch
            {
                8 => 1,
                16 => 2,
                32 => 4,
                64 => 8,
                _ => throw new NotSupportedException($"Unsupported bit depth: {bpp}."),
            };
        }

        public static int SaveToTif(VExportCIE export)
        {
            try
            {
                return SaveToTifCore(export);
            }
            catch (Exception ex)
            {
                Log.Error("导出 CIE 图像失败。", ex);
                return -1;
            }
        }

        internal static void SaveToTifOrThrow(VExportCIE export)
        {
            SaveToTifCore(export);
        }

        internal static string ResolveSaveDirectory(string? savePath, string filePath)
        {
            string? candidate = savePath?.Trim();
            if (string.IsNullOrWhiteSpace(candidate))
                candidate = Path.GetDirectoryName(Path.GetFullPath(filePath));
            if (string.IsNullOrWhiteSpace(candidate))
                candidate = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrWhiteSpace(candidate))
                throw new InvalidOperationException("No export directory is available.");

            string fullPath = Path.GetFullPath(candidate);
            Directory.CreateDirectory(fullPath);
            return fullPath;
        }

        private static int SaveToTifCore(VExportCIE export)
        {
            ArgumentNullException.ThrowIfNull(export);
            string fileName = export.FilePath;
            string savePath = ResolveSaveDirectory(export.SavePath, fileName);
            string name = export.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("The export file name cannot be empty.");
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new InvalidOperationException($"The export file name contains invalid characters: {name}");

            int index = CVFileUtil.ReadCIEFileHeader(fileName, out CVCIEFile cvcie);
            if (index < 0)
                throw new InvalidDataException($"Unable to read the CIE file header: {fileName}");

            string extension = Path.GetExtension(fileName);
            cvcie.FileExtType = extension.Equals(".cvraw", StringComparison.OrdinalIgnoreCase)
                ? CVType.Raw
                : extension.Equals(".cvsrc", StringComparison.OrdinalIgnoreCase)
                    ? CVType.Src
                    : CVType.CIE;

            Mat src;
            int exportedCount = 0;
            void SaveImage(Mat image, string suffix)
            {
                SaveTo(export, image, Path.Combine(savePath, name + suffix));
                exportedCount++;
            }

            switch (cvcie.FileExtType)
            {
                case CVType.Raw:
                    if (export.IsExportSrc)
                    {
                        if (!CVFileUtil.ReadCIEFileData(fileName, ref cvcie, index))
                            throw new InvalidDataException($"Unable to read the CIE image data: {fileName}");
                        using (src = CreateMatFromCVCIEFile(cvcie))
                        {
                            SaveImage(src, "Src");
                        }
                    }
                    break;
                case CVType.Src:
                    if (export.IsExportSrc)
                    {
                        if (!CVFileUtil.ReadCIEFileData(fileName, ref cvcie, index))
                            throw new InvalidDataException($"Unable to read the CIE image data: {fileName}");
                        using (src = CreateMatFromCVCIEFile(cvcie))
                        {
                            SaveImage(src, "Src");
                        }
                    }
                    break;
                case CVType.CIE:

                    if (export.IsExportSrc)
                    {
                        if (cvcie.SrcFileName != null)
                        {
                            cvcie.SrcFileName = Path.Combine(Path.GetDirectoryName(fileName) ?? string.Empty, cvcie.SrcFileName);
                            if (File.Exists(cvcie.SrcFileName) && CVFileUtil.IsCIEFile(cvcie.SrcFileName))
                            {
                                if (CVFileUtil.Read(cvcie.SrcFileName, out CVCIEFile cvraw))
                                {
                                    using (src = CreateMatFromCVCIEFile(cvraw))
                                    {
                                        SaveImage(src, "_Src");
                                    }
                                }
                            }
                        }

                    }
                    if (CVFileUtil.ReadCIEFileData(fileName, ref cvcie, index))
                    {
                        if (cvcie.Channels == 1)
                        {
                            if (export.IsExportChannelY)
                            {
                                using (src = CreateSingleChannelMat(cvcie, cvcie.Data))
                                {
                                    SaveImage(src, "_Y");
                                }
                            }
                        }
                        else if (cvcie.Channels == 3)
                        {
                            if (export.IsExportChannelX)
                            {
                                byte[] data = CopyChannelData(cvcie, 0);
                                using (src = CreateSingleChannelMat(cvcie, data))
                                {
                                    SaveImage(src, "_X");
                                }
                            }
                            if (export.IsExportChannelY)
                            {
                                byte[] data = CopyChannelData(cvcie, 1);
                                using (src = CreateSingleChannelMat(cvcie, data))
                                {
                                    SaveImage(src, "_Y");
                                }

                            }
                            if (export.IsExportChannelZ)
                            {
                                byte[] data = CopyChannelData(cvcie, 2);
                                using (src = CreateSingleChannelMat(cvcie, data))
                                {
                                    SaveImage(src, "_Z");
                                }
                            }
                        }
                    }
                    break;
                case CVType.Calibration:
                    break;
                case CVType.Tif:
                    break;
                default:
                    break;
            }
            if (exportedCount == 0)
                throw new InvalidOperationException("No image channel was selected for export.");
            return 0;
        }

        public VExportCIE(string filePath)
            : this(filePath, MruPathService.CreateLocal("recent-image-export-locations.json"))
        {
        }

        internal VExportCIE(string filePath, MruPathService recentExportLocations)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentNullException.ThrowIfNull(recentExportLocations);
            RecentExportLocations = recentExportLocations;
            FilePath = filePath;
            string extension = Path.GetExtension(filePath);
            FileExtType = extension.Equals(".cvraw", StringComparison.OrdinalIgnoreCase)
                ? CVType.Raw
                : extension.Equals(".cvsrc", StringComparison.OrdinalIgnoreCase)
                    ? CVType.Src
                    : CVType.CIE;
            if (FileExtType == CVType.CIE)
            {
                IsExportChannelX = true;
                IsExportChannelY = true;
                IsExportChannelZ = true;

                if (CVFileUtil.ReadCIEFileHeader(filePath, out CVCIEFile cVCIEFile) > 0 && !string.IsNullOrEmpty(cVCIEFile.SrcFileName))
                {
                    if (!File.Exists(cVCIEFile.SrcFileName))
                        cVCIEFile.SrcFileName = Path.Combine(Path.GetDirectoryName(filePath) ?? string.Empty, Path.GetFileNameWithoutExtension(filePath) + ".cvraw");
                    if (File.Exists(cVCIEFile.SrcFileName))
                    {
                        IsCanExportSrc = CVFileUtil.ReadCIEFileHeader(cVCIEFile.SrcFileName, out _CVCIEFile) > 0;
                        IsChannelOne = _CVCIEFile.Channels == 0;
                    }
                }
                _CVCIEFile = cVCIEFile;

            }
            else if (FileExtType == CVType.Raw)
            {
                IsCanExportSrc = CVFileUtil.ReadCIEFileHeader(filePath, out _CVCIEFile) > 0;
                IsChannelOne = CVCIEFile.Channels == 0;
            }

            RefreshRecentExportLocations();
            if (RecentImageSaveList.Count == 0)
            {
                string defaultPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (!Directory.Exists(defaultPath))
                    defaultPath = Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? defaultPath;
                SavePath = defaultPath;
                RememberExportLocation(defaultPath);
            }
            else
            {
                SavePath = RecentImageSaveList[0];
            }

            Name = Path.GetFileNameWithoutExtension(FilePath);
        }



        public string CoverFilePath { get; set; }


        public int Rows { get => _CVCIEFile.Rows; }
        public int Cols { get => _CVCIEFile.Cols; }
        public int Channels { get => _CVCIEFile.Channels; }
        public float Gain { get => _CVCIEFile.Gain; }
        public float[] Exp { get => _CVCIEFile.Exp; }

        public ImageFormat ExportImageFormat
        {
            get => _ExportImageFormat;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                if (_ExportImageFormat.Guid == value.Guid)
                    return;
                _ExportImageFormat = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsTiff));
                OnPropertyChanged(nameof(IsPng));
                OnPropertyChanged(nameof(IsJpeg));
                OnPropertyChanged(nameof(Compression));
            }
        }
        private ImageFormat _ExportImageFormat = ImageFormat.Tiff;

        public bool IsTiff => IsFormat(ExportImageFormat, ImageFormat.Tiff);
        public bool IsPng => IsFormat(ExportImageFormat, ImageFormat.Png);
        public bool IsJpeg => IsFormat(ExportImageFormat, ImageFormat.Jpeg);

        public CVCIEFile CVCIEFile { get => _CVCIEFile; }
        private CVCIEFile _CVCIEFile;

        public MruPathService RecentExportLocations { get; }

        public ObservableCollection<string> RecentImageSaveList { get; } = new();

        public void RememberExportLocation(string path)
        {
            if (!RecentExportLocations.Touch(path))
                return;
            RefreshRecentExportLocations();
            SavePath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim()));
        }

        private void RefreshRecentExportLocations()
        {
            RecentImageSaveList.Clear();
            foreach (MruPathEntry entry in RecentExportLocations.Items)
            {
                if (Directory.Exists(entry.Path))
                    RecentImageSaveList.Add(entry.Path);
            }
        }


        public bool IsCVRaw { get => FileExtType == CVType.Raw; }

        public bool IsCVCIE { get => FileExtType == CVType.CIE; }

        public bool IsChannelOne { get; set; }


        public string FilePath { get => _FilePath; set { _FilePath = value; OnPropertyChanged(); } }
        private string _FilePath;

        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name;

        public string SavePath { get => _SavePath; set { _SavePath = value; OnPropertyChanged(); } }
        private string _SavePath;

        public CVType FileExtType { get => _FileExtType; set { _FileExtType = value; OnPropertyChanged(); } }
        private CVType _FileExtType;

        public int Compression
        {
            get => IsPng ? PngCompressionLevel : IsJpeg ? JpegQuality : TiffCompression;
            set
            {
                if (IsPng)
                    PngCompressionLevel = value;
                else if (IsJpeg)
                    JpegQuality = value;
                else
                    TiffCompression = value;
            }
        }

        public int TiffCompression
        {
            get => _TiffCompression;
            set
            {
                _TiffCompression = value == 0 ? DefaultTiffCompression : value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Compression));
            }
        }
        private int _TiffCompression = DefaultTiffCompression;

        public int PngCompressionLevel
        {
            get => _PngCompressionLevel;
            set
            {
                _PngCompressionLevel = Math.Clamp(value, 0, 9);
                OnPropertyChanged();
                OnPropertyChanged(nameof(Compression));
            }
        }
        private int _PngCompressionLevel = DefaultPngCompressionLevel;

        public int JpegQuality
        {
            get => _JpegQuality;
            set
            {
                _JpegQuality = Math.Clamp(value, 0, 100);
                OnPropertyChanged();
                OnPropertyChanged(nameof(Compression));
            }
        }
        private int _JpegQuality = DefaultJpegQuality;

        public bool IsExportChannelX { get => _IsExportChannelX; set { _IsExportChannelX = value; OnPropertyChanged(); } }
        private bool _IsExportChannelX;

        public bool IsExportChannelY { get => _IsExportChannelY; set { _IsExportChannelY = value; OnPropertyChanged(); } }
        private bool _IsExportChannelY;

        public bool IsExportChannelZ { get => _IsExportChannelZ; set { _IsExportChannelZ = value; OnPropertyChanged(); } }
        private bool _IsExportChannelZ;

        public bool IsCanExportSrc { get; set; }

        public bool IsExportSrc { get => _IsExportSrc; set { _IsExportSrc = value; OnPropertyChanged(); } }
        private bool _IsExportSrc = true;
        public bool IsExportChannelR { get => _IsExportChannelR; set { _IsExportChannelR = value; OnPropertyChanged(); } }
        private bool _IsExportChannelR;

        public bool IsExportChannelG { get => _IsExportChannelG; set { _IsExportChannelG = value; OnPropertyChanged(); } }
        private bool _IsExportChannelG;

        public bool IsExportChannelB { get => _IsExportChannelB; set { _IsExportChannelB = value; OnPropertyChanged(); } }
        private bool _IsExportChannelB;
    }
}
