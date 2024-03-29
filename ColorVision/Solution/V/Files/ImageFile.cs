﻿using ColorVision.Common.MVVM;
using ColorVision.Extension;
using ColorVision.Media;
using ColorVision.NativeMethods;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Solution.V.Files
{
    public class ImageFile : ViewModelBase,IFile
    {
        public ImageFile() { }
        public FileInfo FileInfo { get; set; }
        public ContextMenu ContextMenu { get; set; }
        public RelayCommand AttributesCommand { get; set; }

        public ImageFile(FileInfo fileInfo) 
        {
            FileInfo = fileInfo;
            Name = FileInfo.Name;
            FullName = FileInfo.FullName;
            var icon = FileIcon.GetFileIcon(fileInfo.FullName);
            if (icon != null)
                Icon = icon.ToImageSource();

            ContextMenu = new ContextMenu();

            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resource.Open, Command = new RelayCommand((e) => Open()) });

            AttributesCommand = new RelayCommand(a => FileProperties.ShowFileProperties(FullName), a => true);
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resource.Property, Command = AttributesCommand });
        }

        public string Name { get; set; }
        public string FullName { get; set; }
        public string ToolTip { get; set; }
        public ImageSource Icon { get; set; }

        public string FileSize { get => _FileSize; set { _FileSize = value; NotifyPropertyChanged(); } }
        private string _FileSize;


        public void Copy()
        {
            throw new System.NotImplementedException();
        }

        public void Delete()
        {
            throw new System.NotImplementedException();
        }

        public void Open()
        {
            ImageView imageView = new ImageView();
            Window window = new Window() { };
            window.Content = imageView;
            _ = RunAsync(imageView);
            window.Show();
        }
        public async Task<Task> RunAsync(ImageView imageView)
        {
            await Task.Delay(100);
            imageView.OpenImage(FileInfo.FullName); 
            return Task.CompletedTask;
        }


        public void ReName()
        {
            throw new System.NotImplementedException();
        }
    }



    public class CommonFile : ViewModelBase, IFile
    {
        public CommonFile() { }
        public FileInfo FileInfo { get; set; }
        public ContextMenu ContextMenu { get; set; }
        public CommonFile(FileInfo fileInfo)
        {
            FileInfo = fileInfo;
            Name = FileInfo.Name;
            FullName = FileInfo.FullName;
            var icon = FileIcon.GetFileIcon(fileInfo.FullName);
            if (icon != null)
                Icon = icon.ToImageSource();
            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resource.Open, Command = new RelayCommand(a => Open()) });
            ContextMenu.Items.Add(new MenuItem() { Header = "属性", Command = new RelayCommand(a => FileProperties.ShowFileProperties(FullName)) });
        }

        public string Name { get; set; }
        public string FullName { get; set; }
        public string ToolTip { get; set; }
        public ImageSource Icon { get; set; }

        public string FileSize { get => _FileSize; set { _FileSize = value; NotifyPropertyChanged(); } }
        private string _FileSize;


        public void Copy()
        {
            throw new System.NotImplementedException();
        }

        public void Delete()
        {
            throw new System.NotImplementedException();
        }

        public void Open()
        {
            Process.Start("explorer.exe", FullName);
        }

        public void ReName()
        {
            throw new System.NotImplementedException();
        }
    }

}
