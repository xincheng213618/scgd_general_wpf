﻿using ColorVision.Common.Utilities;
using ColorVision.ImageEditor;
using ColorVision.UI;
using System.Windows;

namespace ColorVision.Solution.Imp.CommonImage
{
    public class FileProcessorImage : IFileProcessor
    {
        public string GetExtension() => "图像文件 (*.jpg;*.png;*.jpeg;*.tif;*.bmp)|*.jpg;*.png;*.jpeg;*.tif;*.bmp"; // "cvcie

        public int Order => 3;

        public bool CanProcess(string filePath)
        {
            return Tool.IsImageFile(filePath);
        }
        public bool CanExport(string filePath)
        {
            return false;
        }

        public void Export(string filePath)
        {

        }

        public void Process(string filePath)
        {
            ImageView imageView = new();
            Window window = new() { Title = "快速预览" };
            window.Content = imageView;
            imageView.OpenImage(filePath);
            window.Show();
        }
    }


}
