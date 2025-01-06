using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using ColorVision.Engine.Properties;
using ColorVision.Engine.Media;
using ColorVision.Net;
using ColorVision.Solution;
using ColorVision.Solution.V.Files;
using ColorVision.UI.Menus;
using OpenCvSharp;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Engine.Impl.CVFile
{

    public class FileCVCIE : ViewModelBase, IFileMeta, IContextMenuProvider
    {
        public FileInfo FileInfo { get; set; }

        public string Extension { get => ".cvraw|.cvcie"; }

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
            FullName = FileInfo.FullName;
            Icon = FileIcon.GetFileIconImageSource(fileInfo.FullName);

            ExportCommand = new RelayCommand(a => Export(), a => true);
            ExportBMPCommand = new RelayCommand(a => ExportAS(ImageFormat.Bmp), A => true);
            ExportTIFFCommand = new RelayCommand(a => ExportAS(ImageFormat.Tiff), A => true);
            ExportPNGCommand = new RelayCommand(a => ExportAS(ImageFormat.Png), A => true);
        }

        public string Name { get; set; }
        public string FullName { get; set; }
        public string ToolTip { get; set; }
        public ImageSource? Icon { get; set; }

        public string FileSize { get => FileInfo.Length.ToString(); set { NotifyPropertyChanged(); } }

        public void Export()
        {
            new ExportCVCIE(FullName) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
        public void ExportAS(ImageFormat imageFormat)
        {
            int index = CVFileUtil.ReadCIEFileHeader(FullName, out CVCIEFile cvcie);
            if (index < 0) return;
            cvcie.FileExtType = FullName.Contains(".cvraw") ? CVType.Raw : FullName.Contains(".cvsrc") ? CVType.Src : CVType.CIE;

            CVFileUtil.ReadCIEFileData(FullName, ref cvcie, index);
            var src = Mat.FromPixelData(cvcie.cols, cvcie.rows, MatType.MakeType(cvcie.Depth, cvcie.channels), cvcie.data);

            System.Windows.Forms.SaveFileDialog dialog = new System.Windows.Forms.SaveFileDialog();
            dialog.FileName = Path.GetFileNameWithoutExtension(FullName) + $".{imageFormat}";
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


        public IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            return new List<MenuItemMetadata>()
            {
                new MenuItemMetadata()
                {
                    GuidId ="Export",
                    Header = Resources.Export,
                    Order =1,
                    Command = ExportCommand
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

        public void Open()
        {
            if (File.Exists(FileInfo.FullName))
            {
                SolutionProcessCVCIE fileControl = new SolutionProcessCVCIE() { Name = Name, FullName = FileInfo.FullName, IconSource = Icon };
                SolutionManager.GetInstance().OpenFileWindow(fileControl);
            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到文件", "ColorVision");
            }

        }


        public void Copy()
        {
            throw new System.NotImplementedException();
        }

        public void Delete()
        {
            throw new System.NotImplementedException();
        }

        public void ReName()
        {
            throw new System.NotImplementedException();
        }


    }



}
