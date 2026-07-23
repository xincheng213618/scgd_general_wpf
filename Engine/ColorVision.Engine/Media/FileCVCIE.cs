using ColorVision.Common.MVVM;
using ColorVision.Engine.Properties;
using ColorVision.Engine.Media;
using ColorVision.FileIO;
using ColorVision.UI.Menus;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Windows;
using ColorVision.Solution.Explorer;
using System.ComponentModel;
using SolutionFileNode = ColorVision.Solution.Explorer.FileNode;

namespace ColorVision.Engine.Impl.CVFile
{
    internal sealed class CvcieFileActions
    {
        private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".cvcie",
            ".cvraw",
        };

        public FileInfo FileInfo { get; }
        public RelayCommand ExportCommand { get; set; }
        public RelayCommand ExportBMPCommand { get; set; }
        public RelayCommand ExportTIFFCommand { get; set; }
        public RelayCommand ExportPNGCommand { get; set; }
        
        public CvcieFileActions(FileInfo fileInfo)
        {
            FileInfo = fileInfo;
            ExportCommand = new RelayCommand(a => Export(), a => true);
            ExportBMPCommand = new RelayCommand(a => ExportAS(ImageFormat.Bmp), A => true);
            ExportTIFFCommand = new RelayCommand(a => ExportAS(ImageFormat.Tiff), A => true);
            ExportPNGCommand = new RelayCommand(a => ExportAS(ImageFormat.Png), A => true);
        }

        public static bool Supports(FileInfo fileInfo)
        {
            return fileInfo.Exists && SupportedExtensions.Contains(fileInfo.Extension);
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
            return context.PrimaryNode is SolutionFileNode fileNode
                && CvcieFileActions.Supports(fileNode.FileInfo);
        }

        public IEnumerable<MenuItemMetadata> CreateMenuItems(SolutionMenuContext context)
        {
            var actions = new CvcieFileActions(((SolutionFileNode)context.PrimaryNode).FileInfo);
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
                    Command = actions.ExportBMPCommand,
                },
                new MenuItemMetadata
                {
                    OwnerGuid = "Export",
                    GuidId = "ExportTIF",
                    Header = ColorVision.Engine.Properties.Resources.ExportTo + " TIFF",
                    Order = 3,
                    Command = actions.ExportTIFFCommand,
                },
                new MenuItemMetadata
                {
                    OwnerGuid = "Export",
                    GuidId = "ExportPNG",
                    Header = ColorVision.Engine.Properties.Resources.ExportTo + " PNG",
                    Order = 3,
                    Command = actions.ExportPNGCommand,
                },
                new MenuItemMetadata
                {
                    OwnerGuid = "Export",
                    GuidId = "ExportAs",
                    Header = ColorVision.Engine.Properties.Resources.ExportTo,
                    Order = 1,
                    Command = actions.ExportCommand,
                },
            ];
        }
    }
}
