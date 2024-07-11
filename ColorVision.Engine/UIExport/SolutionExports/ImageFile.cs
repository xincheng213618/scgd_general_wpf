using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Media;
using ColorVision.Solution;
using ColorVision.Solution.V.Files;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ColorVision.Engine.UIExport.SolutionExports
{
    public class TextFileOpen : IFileControl
    {
        public string Name { get; set; }
        public ImageSource IconSource { get; set; }

        public Control UserControl => richTextBox;

        public string GuidId => Tool.GetMD5(FullName);

        public string FullName { get; set; }

        public void Close()
        {

        }
        private RichTextBox richTextBox = new RichTextBox();

        public void Open()
        {
            const int bufferSize = 1024; // 每次读取的字节数，可以根据需要调整
            if (File.Exists(FullName))
            {
                TextRange textRange = new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd);
                using (FileStream fileStream = new FileStream(FullName, FileMode.Open, FileAccess.Read))
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
        }
    }

    public class TextFile : ViewModelBase, IFileMeta
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

            TextFileOpen textFileOpen = new TextFileOpen() { Name = FileInfo.Name,FullName = FileInfo.FullName };
            SolutionManager.GetInstance().OpenFileWindow(textFileOpen);
        }


        public void ReName()
        {
            throw new NotImplementedException();
        }

    }

    public class ImageFileOpen : IFileControl
    {
        public ImageView ImageView { get; set; } = ImageView.GetInstance();

        public string Name { get; set; }

        public ImageSource IconSource { get; set; }
        public Control UserControl => ImageView;

        public string FullName { get; set; }

        public string GuidId => Tool.GetMD5(FullName);

        public void Close()
        {
            ImageView.ToolBarTop.ClearImage();
        }

        public virtual void Open()
        {
            ImageView.OpenImage(FullName);
        }
    }


    public class ImageFile : ViewModelBase, IFileMeta
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
            ImageFileOpen fileControl = new ImageFileOpen() { Name = Name ,FullName = FileInfo.FullName, IconSource = Icon};
            SolutionManager.GetInstance().OpenFileWindow(fileControl);
        }


        public void ReName()
        {
            throw new NotImplementedException();
        }
    }

}
