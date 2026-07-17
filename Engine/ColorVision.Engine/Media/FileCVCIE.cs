using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using ColorVision.Engine.Properties;
using ColorVision.Engine.Media;
using ColorVision.FileIO;
using ColorVision.UI.Menus;
using OpenCvSharp;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Windows;
using ColorVision.Solution.Explorer;
using ColorVision.Solution.FileMeta;
using System.ComponentModel;
using SolutionFileNode = ColorVision.Solution.Explorer.FileNode;

namespace ColorVision.Engine.Impl.CVFile
{
    [FileExtension(".cvraw", ".cvcie")]
    public class FileCVCIE : FileMetaBase
    {
        public override int Order => 99;
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
            using var src = Mat.FromPixelData(cvcie.Rows, cvcie.Cols, MatType.MakeType(cvcie.Depth, cvcie.Channels), cvcie.Data);

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
                        using (Mat bmpImage = VExportCIE.CreateBmpCompatibleMat(src))
                        {
                            bmpImage.SaveImage(dialog.FileName);
                        }
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
    }

    [SolutionMenuContribution(priority: 250)]
    public sealed class FileCVCIEMenuContribution : ISolutionMenuContribution
    {
        public string Id => "colorvision.engine.cvcie-file-actions";
        public SolutionMenuSelectionPolicy SelectionPolicy => SolutionMenuSelectionPolicy.SingleOnly;

        public bool IsApplicable(SolutionMenuContext context)
        {
            return context.PrimaryNode is SolutionFileNode
            {
                FileMeta: FileCVCIE,
                FileInfo.Exists: true,
            };
        }

        public IEnumerable<MenuItemMetadata> CreateMenuItems(SolutionMenuContext context)
        {
            var fileMeta = (FileCVCIE)((SolutionFileNode)context.PrimaryNode).FileMeta;
            return
            [
                new MenuItemMetadata
                {
                    GuidId = "Export",
                    Header = Resources.Export,
                    Order = 50,
                },
                new MenuItemMetadata
                {
                    OwnerGuid = "Export",
                    GuidId = "ExportBmp",
                    Header = ColorVision.Engine.Properties.Resources.ExportTo + " BMP",
                    Order = 2,
                    Command = fileMeta.ExportBMPCommand,
                },
                new MenuItemMetadata
                {
                    OwnerGuid = "Export",
                    GuidId = "ExportTIF",
                    Header = ColorVision.Engine.Properties.Resources.ExportTo + " TIFF",
                    Order = 3,
                    Command = fileMeta.ExportTIFFCommand,
                },
                new MenuItemMetadata
                {
                    OwnerGuid = "Export",
                    GuidId = "ExportPNG",
                    Header = ColorVision.Engine.Properties.Resources.ExportTo + " PNG",
                    Order = 3,
                    Command = fileMeta.ExportPNGCommand,
                },
                new MenuItemMetadata
                {
                    OwnerGuid = "Export",
                    GuidId = "ExportAs",
                    Header = ColorVision.Engine.Properties.Resources.ExportTo,
                    Order = 1,
                    Command = fileMeta.ExportCommand,
                },
            ];
        }
    }
}
