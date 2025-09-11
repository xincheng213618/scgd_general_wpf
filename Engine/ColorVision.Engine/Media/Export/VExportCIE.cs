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
            fileName = fileName + export.ExportImageFormat.ToString().ToLower(CultureInfo.CurrentCulture);
            export.CoverFilePath = fileName;
            if (export.ExportImageFormat == ImageFormat.Tiff)
            {
                if (export.Compression == 0)
                {
                    export.Compression = 1;
                }
                src.SaveImage(fileName, new ImageEncodingParam(ImwriteFlags.TiffCompression, export.Compression));
            }
            else if (export.ExportImageFormat == ImageFormat.Bmp)
            {
                src.SaveImage(fileName);
            }
            else if (export.ExportImageFormat == ImageFormat.Png)
            {
                if (export.Compression == 0)
                {
                    export.Compression = 3;
                }

                src.SaveImage(fileName, new ImageEncodingParam(ImwriteFlags.PngCompression, 3));
            }
            else if (export.ExportImageFormat == ImageFormat.Jpeg)
            {
                if (export.Compression == 0)
                {
                    export.Compression = 95;
                }
                src.SaveImage(fileName, new ImageEncodingParam(ImwriteFlags.JpegQuality, 95));
            }
        }

        public static int SaveToTif(VExportCIE export)
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
                        src = Mat.FromPixelData(cvcie.cols, cvcie.rows, MatType.MakeType(cvcie.Depth, cvcie.channels), cvcie.data);
                        SaveTo(export, src, SavePath + "\\" + Name + "Src.");
                    }
                    break;
                case CVType.Src:
                    if (export.IsExportSrc)
                    {
                        CVFileUtil.ReadCIEFileData(FileName, ref cvcie, index);
                        src = Mat.FromPixelData(cvcie.cols, cvcie.rows, MatType.MakeType(cvcie.Depth, cvcie.channels), cvcie.data);
                        SaveTo(export, src, SavePath + "\\" + Name + "Src.");
                    }
                    break;
                case CVType.CIE:

                    if (export.IsExportSrc)
                    {
                        cvcie.srcFileName = Path.Combine(Path.GetDirectoryName(FileName) ?? string.Empty, cvcie.srcFileName);
                        if (File.Exists(cvcie.srcFileName) && CVFileUtil.IsCIEFile(cvcie.srcFileName))
                        {
                            if (CVFileUtil.Read(cvcie.srcFileName, out CVCIEFile cvraw))
                            {
                                src = Mat.FromPixelData(cvraw.cols, cvraw.rows, MatType.MakeType(cvraw.Depth, cvraw.channels), cvraw.data);
                                SaveTo(export, src, SavePath + "\\" + Name + "_Src.");
                            }
                        }
                    }
                    if (CVFileUtil.ReadCIEFileData(FileName, ref cvcie, index))
                    {
                        if (cvcie.channels == 1)
                        {
                            if (export.IsExportChannelY)
                            {
                                src = Mat.FromPixelData(cvcie.cols, cvcie.rows, MatType.MakeType(MatType.CV_32F, 1), cvcie.data);
                                SaveTo(export, src, SavePath + "\\" + Name + "_Y.");
                            }
                        }
                        else if (cvcie.channels == 3)
                        {
                            int len = cvcie.cols * cvcie.rows * cvcie.bpp / 8;

                            if (export.IsExportChannelX)
                            {
                                byte[] data = new byte[len];
                                Buffer.BlockCopy(cvcie.data, 0 * len, data, 0, len);
                                src = Mat.FromPixelData(cvcie.cols, cvcie.rows, MatType.MakeType(MatType.CV_32F, 1), data);
                                SaveTo(export, src, SavePath + "\\" + Name + "_X.");
                            }
                            if (export.IsExportChannelY)
                            {
                                byte[] data = new byte[len];
                                Buffer.BlockCopy(cvcie.data, 1 * len, data, 0, len);
                                src = Mat.FromPixelData(cvcie.cols, cvcie.rows, MatType.MakeType(MatType.CV_32F, 1), data);
                                SaveTo(export, src, SavePath + "\\" + Name + "_Y.");

                            }
                            if (export.IsExportChannelZ)
                            {
                                byte[] data = new byte[len];
                                Buffer.BlockCopy(cvcie.data, 2 * len, data, 0, len);
                                src = Mat.FromPixelData(cvcie.cols, cvcie.rows, MatType.MakeType(MatType.CV_32F, 1), data);
                                SaveTo(export, src, SavePath + "\\" + Name + "_Z.");
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

                if (CVFileUtil.ReadCIEFileHeader(filePath, out CVCIEFile cVCIEFile) > 0 && !string.IsNullOrEmpty(cVCIEFile.srcFileName))
                {
                    if (!File.Exists(cVCIEFile.srcFileName))
                        cVCIEFile.srcFileName = Path.Combine(Path.GetDirectoryName(filePath) ?? string.Empty, Path.GetFileNameWithoutExtension(filePath) + ".cvraw");
                    if (File.Exists(cVCIEFile.srcFileName))
                    {
                        IsCanExportSrc = CVFileUtil.ReadCIEFileHeader(cVCIEFile.srcFileName, out _CVCIEFile) > 0;
                        IsChannelOne = _CVCIEFile.channels == 0;
                    }
                }

            }
            else if (FileExtType == CVType.Raw)
            {
                IsCanExportSrc = CVFileUtil.ReadCIEFileHeader(filePath, out _CVCIEFile) > 0;
                IsChannelOne = CVCIEFile.channels == 0;
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


        public int Rows { get => _CVCIEFile.rows; }
        public int Cols { get => _CVCIEFile.cols; }
        public int Channels { get => _CVCIEFile.channels; }
        public float Gain { get => _CVCIEFile.gain; }
        public float[] Exp { get => _CVCIEFile.exp; }

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
