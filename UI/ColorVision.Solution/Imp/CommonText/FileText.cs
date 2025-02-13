using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using ColorVision.Solution;
using ColorVision.Solution.V.Files;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Solution.Imp.CommonText
{
    public class FileText : FileMetaBase
    {
        public override int Order => 1;
        public FileText() { }
        public RelayCommand AttributesCommand { get; set; }
        public override string Extension { get => ".txt|.cs"; }

        public FileText(FileInfo fileInfo)
        {
            FileInfo = fileInfo;
            Name = FileInfo.Name;
            Icon = FileIcon.GetFileIconImageSource(fileInfo.FullName);
        }
    }

}
