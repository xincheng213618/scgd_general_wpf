using ColorVision.Common.MVVM;
using ColorVision.FileIO;
using ColorVision.Solution.RecentFile;
using OpenCvSharp;
using System;
using System.Collections.ObjectModel;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;

namespace ColorVision.Engine.Media
{
    public class VExportCIE : ViewModelBase
    {

        public static void SaveTo(VExportCIE export, Mat src, string fileName)
        {
            ArgumentNullException.ThrowIfNull(export);
            ArgumentNullException.ThrowIfNull(src);
            if (src.Empty())
            {
                throw new InvalidOperationException("Cannot export an empty image.");
            }

            fileName = fileName + export.ExportImageFormat.ToString().ToLower(CultureInfo.CurrentCulture);
            export.CoverFilePath = fileName;
            using Mat image = src.Clone();
            if (export.ExportImageFormat == ImageFormat.Tiff)
            {
                if (export.Compression == 0)
                {
                    export.Compression = 1;
                }
                image.SaveImage(fileName, new ImageEncodingParam(ImwriteFlags.TiffCompression, export.Compression));
            }
            else if (export.ExportImageFormat == ImageFormat.Bmp)
            {
                using Mat bmpImage = CreateBmpCompatibleMat(image);
                bmpImage.SaveImage(fileName);
            }
            else if (export.ExportImageFormat == ImageFormat.Png)
            {
                int compression = export.Compression == 0 ? 3 : Math.Clamp(export.Compression, 0, 9);
                image.SaveImage(fileName, new ImageEncodingParam(ImwriteFlags.PngCompression, compression));
            }
            else if (export.ExportImageFormat == ImageFormat.Jpeg)
            {
                int quality = export.Compression == 0 ? 95 : Math.Clamp(export.Compression, 0, 100);
                image.SaveImage(fileName, new ImageEncodingParam(ImwriteFlags.JpegQuality, quality));
            }
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
                System.Diagnostics.Debug.WriteLine($"[VExportCIE.SaveToTif] {ex}");
                return -1;
            }
        }

        private static int SaveToTifCore(VExportCIE export)
        {
            string FileName = export.FilePath;
            string SavePath = export.SavePath;
            string Name = export.Name;

            int index = CVFileUtil.ReadCIEFileHeader(FileName, out CVCIEFile cvcie);
            if (index < 0) return -1;
            cvcie.FileExtType = FileName.Contains(".cvraw") ? CVType.Raw : FileName.Contains(".cvsrc") ? CVType.Src : CVType.CIE;

            Mat src;
            switch (cvcie.FileExtType)
            {
                case CVType.Raw:
                    if (export.IsExportSrc)
                    {
                        CVFileUtil.ReadCIEFileData(FileName, ref cvcie, index);
                        using (src = CreateMatFromCVCIEFile(cvcie))
                        {
                            SaveTo(export, src, SavePath + "\\" + Name + "Src.");
                        }
                    }
                    break;
                case CVType.Src:
                    if (export.IsExportSrc)
                    {
                        CVFileUtil.ReadCIEFileData(FileName, ref cvcie, index);
                        using (src = CreateMatFromCVCIEFile(cvcie))
                        {
                            SaveTo(export, src, SavePath + "\\" + Name + "Src.");
                        }
                    }
                    break;
                case CVType.CIE:

                    if (export.IsExportSrc)
                    {
                        if (cvcie.SrcFileName != null)
                        {
                            cvcie.SrcFileName = Path.Combine(Path.GetDirectoryName(FileName) ?? string.Empty, cvcie.SrcFileName);
                            if (File.Exists(cvcie.SrcFileName) && CVFileUtil.IsCIEFile(cvcie.SrcFileName))
                            {
                                if (CVFileUtil.Read(cvcie.SrcFileName, out CVCIEFile cvraw))
                                {
                                    using (src = CreateMatFromCVCIEFile(cvraw))
                                    {
                                        SaveTo(export, src, SavePath + "\\" + Name + "_Src.");
                                    }
                                }
                            }
                        }

                    }
                    if (CVFileUtil.ReadCIEFileData(FileName, ref cvcie, index))
                    {
                        if (cvcie.Channels == 1)
                        {
                            if (export.IsExportChannelY)
                            {
                                using (src = CreateSingleChannelMat(cvcie, cvcie.Data))
                                {
                                    SaveTo(export, src, SavePath + "\\" + Name + "_Y.");
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
                                    SaveTo(export, src, SavePath + "\\" + Name + "_X.");
                                }
                            }
                            if (export.IsExportChannelY)
                            {
                                byte[] data = CopyChannelData(cvcie, 1);
                                using (src = CreateSingleChannelMat(cvcie, data))
                                {
                                    SaveTo(export, src, SavePath + "\\" + Name + "_Y.");
                                }

                            }
                            if (export.IsExportChannelZ)
                            {
                                byte[] data = CopyChannelData(cvcie, 2);
                                using (src = CreateSingleChannelMat(cvcie, data))
                                {
                                    SaveTo(export, src, SavePath + "\\" + Name + "_Z.");
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
            return 0;
        }

        public VExportCIE(string filePath)
        {
            FilePath = filePath;
            FileExtType = filePath.Contains(".cvraw") ? CVType.Raw : filePath.Contains(".cvsrc") ? CVType.Src : CVType.CIE;
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

            if (RecentImage.RecentFiles.Count == 0)
            {
                RecentImage.InsertFile(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            }
            SavePath = RecentImage.RecentFiles[0];
            RecentImageSaveList = new ObservableCollection<string>(RecentImage.RecentFiles);

            Name = Path.GetFileNameWithoutExtension(FilePath);
        }



        public string CoverFilePath { get; set; }


        public int Rows { get => _CVCIEFile.Rows; }
        public int Cols { get => _CVCIEFile.Cols; }
        public int Channels { get => _CVCIEFile.Channels; }
        public float Gain { get => _CVCIEFile.Gain; }
        public float[] Exp { get => _CVCIEFile.Exp; }

        public ImageFormat ExportImageFormat { get; set; } = ImageFormat.Tiff;

        public CVCIEFile CVCIEFile { get => _CVCIEFile; }
        private CVCIEFile _CVCIEFile;

        public RecentFileList RecentImage { get; set; } = new RecentFileList() { Persister = new RegistryPersister("Software\\ColorVision\\RecentImageSaveList") };

        public ObservableCollection<string> RecentImageSaveList { get; set; }


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

        public int Compression { get => _Compression; set { _Compression = value; OnPropertyChanged(); } }
        private int _Compression ;

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
