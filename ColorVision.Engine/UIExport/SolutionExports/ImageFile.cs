using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using ColorVision.Engine.Media;
using ColorVision.UI.Extension;
using ColorVision.Solution.V.Files;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ColorVision.Solution;
using System.Drawing;
using System.Windows.Documents;

namespace ColorVision.Engine.UIExport.SolutionExports
{
    public class TextFileOpen : IFileControl
    {
        public string Name { get; set; }

        public Control UserControl { get; set; }

        public void Close()
        {
            UserControl = null;
        }

        public void Open()
        {

        }
    }

    public class TextFile : ViewModelBase, IFile
    {
        public TextFile() { }
        public FileInfo FileInfo { get; set; }
        public ContextMenu ContextMenu { get; set; }
        public RelayCommand AttributesCommand { get; set; }
        public string Extension { get => ".txt|.cs"; }

        public TextFile(FileInfo fileInfo)
        {
            FileInfo = fileInfo;
            Name = FileInfo.Name;
            FullName = FileInfo.FullName;
            var icon = FileIcon.GetFileIcon(fileInfo.FullName);
            if (icon != null)
                Icon = icon.ToImageSource();
        }
        public string Name { get; set; }
        public string FullName { get; set; }
        public string ToolTip { get; set; }
        public ImageSource Icon { get; set; }

        public string FileSize { get => _FileSize; set { _FileSize = value; NotifyPropertyChanged(); } }
        private string _FileSize;

        public void Copy()
        {
            throw new NotImplementedException();
        }

        public void Delete()
        {
            try
            {
                File.Delete(FullName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public void Open()
        {
            RichTextBox richTextBox = new RichTextBox();
            const int bufferSize = 1024; // 每次读取的字节数，可以根据需要调整
            if (File.Exists(FileInfo.FullName))
            {
                TextRange textRange = new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd);
                using (FileStream fileStream = new FileStream(FileInfo.FullName, FileMode.Open, FileAccess.Read))
                {
                    using (StreamReader reader = new StreamReader(fileStream))
                    {
                        char[] buffer = new char[bufferSize];
                        int charsRead;
                        while ((charsRead = reader.Read(buffer, 0, bufferSize)) > 0)
                        {
                            string text = new string(buffer, 0, charsRead);
                            richTextBox.AppendText(text);
                        }
                    }
                }
            }
            TextFileOpen textFileOpen = new TextFileOpen() { Name = FileInfo.Name, UserControl = richTextBox };
            SolutionManager.GetInstance().OpenFileWindow(textFileOpen);
        }


        public void ReName()
        {
            throw new NotImplementedException();
        }

    }

    public class ImageFile : ViewModelBase, IFile
    {
        public ImageFile() { }
        public FileInfo FileInfo { get; set; }
        public ContextMenu ContextMenu { get; set; }
        public RelayCommand AttributesCommand { get; set; }
        public string Extension { get => ".jpg|.png|.jpeg|.tif|.bmp"; }
        public ImageFile(FileInfo fileInfo)
        {
            FileInfo = fileInfo;
            Name = FileInfo.Name;
            FullName = FileInfo.FullName;
            var icon = FileIcon.GetFileIcon(fileInfo.FullName);
            if (icon != null)
                Icon = icon.ToImageSource();
        }

        public string Name { get; set; }
        public string FullName { get; set; }
        public string ToolTip { get; set; }
        public ImageSource Icon { get; set; }

        public string FileSize { get => _FileSize; set { _FileSize = value; NotifyPropertyChanged(); } }
        private string _FileSize;


        public void Copy()
        {
            throw new NotImplementedException();
        }

        public void Delete()
        {
            try
            {
                File.Delete(FullName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public void Open()
        {
            CVCIEFileOpen fileControl = new CVCIEFileOpen() { Name = Name };
            fileControl.ImageView.OpenImage(FileInfo.FullName);
            SolutionManager.GetInstance().OpenFileWindow(fileControl);
        }


        public void ReName()
        {
            throw new NotImplementedException();
        }
    }

}
