using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using ColorVision.Engine.Properties;
using ColorVision.Engine.Media;
using ColorVision.Net;
using ColorVision.UI.Menus;
using OpenCvSharp;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Windows;
using ColorVision.Solution.FileMeta;

namespace ColorVision.Engine.Impl.CVFile
{

    public class FileCVCIE : FileMetaBase
    {
        public override int Order => 99;
        public override string Extension { get => ".cvraw|.cvcie"; }
        public override string Name { get; set; }

        public FileCVCIE()
        {
        }
        public RelayCommand ExportCommand { get; set; }
        public RelayCommand ExportBMPCommand { get; set; }
        public RelayCommand ExportTIFFCommand { get; set; }
        public RelayCommand ExportPNGCommand { get; set; }
        
        public FileCVCIE(FileInfo fileInfo)
        {
            FileInfo = fileInfo;
            Name = FileInfo.Name;
            Icon = FileIcon.GetFileIconImageSource(fileInfo.FullName);
            ExportCommand = new RelayCommand(a => Export(), a => true);
            ExportBMPCommand = new RelayCommand(a => ExportAS(ImageFormat.Bmp), A => true);
            ExportTIFFCommand = new RelayCommand(a => ExportAS(ImageFormat.Tiff), A => true);
            ExportPNGCommand = new RelayCommand(a => ExportAS(ImageFormat.Png), A => true);
        }

        public void Export()
        {
            new ExportCVCIE(FileInfo.FullName) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
        public void ExportAS(ImageFormat imageFormat)
        {
            int index = CVFileUtil.ReadCIEFileHeader(FileInfo.FullName, out CVCIEFile cvcie);
            if (index < 0) return;
            cvcie.FileExtType = FileInfo.FullName.Contains(".cvraw") ? CVType.Raw : FileInfo.FullName.Contains(".cvsrc") ? CVType.Src : CVType.CIE;

            CVFileUtil.ReadCIEFileData(FileInfo.FullName, ref cvcie, index);
            var src = Mat.FromPixelData(cvcie.cols, cvcie.rows, MatType.MakeType(cvcie.Depth, cvcie.channels), cvcie.data);

            System.Windows.Forms.SaveFileDialog dialog = new System.Windows.Forms.SaveFileDialog();
            dialog.FileName = Path.GetFileNameWithoutExtension(FileInfo.FullName) + $".{imageFormat}";
            dialog.Filter = "Bitmap Image|*.bmp|PNG Image|*.png|JPEG Image|*.jpg;*.jpeg|TIFF Image|*.tiff|All Files|*.*";
            dialog.RestoreDirectory = true;
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string selectedExt = Path.GetExtension(dialog.FileName).ToLower(CultureInfo.CurrentCulture);
                switch (selectedExt)
                {
                    case ".bmp":
                        src.SaveImage(dialog.FileName);
                        break;
                    case ".png":
                        src.SaveImage(dialog.FileName, new ImageEncodingParam(ImwriteFlags.PngCompression, 3));
                        break;
                    case ".jpg":
                    case ".jpeg":
                        src.SaveImage(dialog.FileName, new ImageEncodingParam(ImwriteFlags.JpegQuality, 95));
                        break;
                    case ".tiff":
                        src.SaveImage(dialog.FileName, new ImageEncodingParam(ImwriteFlags.TiffCompression, 1));
                        break;
                    default:
                        MessageBox.Show("Unsupported file format selected.", "Error");
                        break;
                }
            }
        }


        public override IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            return new List<MenuItemMetadata>()
            {
                new MenuItemMetadata()
                {
                    GuidId ="Export",
                    Header = Resources.Export,
                    Order = 50
                },
                new MenuItemMetadata()
                {
                    OwnerGuid ="Export",
                    GuidId ="ExportBmp",
                    Header = "导出为 BMP",
                    Order =2,
                    Command = ExportBMPCommand,
                },
                new MenuItemMetadata()
                {
                    OwnerGuid ="Export",
                    GuidId ="ExportTIF",
                    Header = "导出为 TIFF",
                    Order =3,
                    Command = ExportTIFFCommand,
                },
                new MenuItemMetadata()
                {
                    OwnerGuid ="Export",
                    GuidId ="ExportPNG",
                    Header = "导出为 PNG",
                    Order =3,
                    Command = ExportPNGCommand,
                },
                new MenuItemMetadata()
                {
                    OwnerGuid ="Export",
                    GuidId ="ExportAs",
                    Header = "导出为",
                    Order =1,
                    Command = ExportCommand
                },
            };

        }
    }



}
