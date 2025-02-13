using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using ColorVision.Solution;
using ColorVision.Solution.V.Files;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Solution.Imp.CommonImage
{
    public class FileImage : FileMetaBase
    {
        public override string Extension { get => ".jpg|.png|.jpeg|.tif|.bmp|.tiff|"; }
        public FileImage() { }

        public FileImage(FileInfo fileInfo)
        {
            FileInfo = fileInfo;
            Name = FileInfo.Name;
            Icon = FileIcon.GetFileIconImageSource(fileInfo.FullName);
        }

        public void Open()
        {
            SolutionProcessImage fileControl = new SolutionProcessImage() { Name = Name, FullName = FileInfo.FullName, IconSource = Icon };
            SolutionManager.GetInstance().OpenFileWindow(fileControl);
        }
    }

}
