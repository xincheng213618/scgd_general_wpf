﻿using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Themes.Controls.Uploads
{
    /// <summary>
    /// Uploads.xaml 的交互逻辑
    /// </summary>
    public partial class UploadControl : UserControl
    {
        public UploadControl()
        {
            InitializeComponent();

            DragEnter += (s, e) =>
            {
                e.Effects = DragDropEffects.Scroll;
                e.Handled = true;
                UploadRec.Stroke = Brushes.Blue;
            };
            DragLeave += (s, e) =>
            {
                UploadRec.Stroke = Brushes.Gray;
            };
        }
        public event EventHandler SelectChaned;


        public static readonly DependencyProperty UploadFileNameProperty = DependencyProperty.Register(nameof(UploadFileNameProperty), typeof(string), typeof(UploadControl), new PropertyMetadata(""));
        public string UploadFileName
        {
            get { return (string)GetValue(UploadFileNameProperty); }
            set { SetValue(UploadFileNameProperty, value); }
        }
       
        public static readonly DependencyProperty UploadFilePathProperty = DependencyProperty.Register(nameof(UploadFilePathProperty), typeof(string), typeof(UploadControl), new PropertyMetadata(""));
        public string UploadFilePath
        {
            get { return (string)GetValue(UploadFilePathProperty); }
            set { SetValue(UploadFilePathProperty, value); }
        }


        public static readonly DependencyProperty FilterProperty = DependencyProperty.Register(nameof(FilterProperty), typeof(string), typeof(UploadControl), new PropertyMetadata("All files (*.*)|*.*"));
        public string Filter
        {
            get { return (string)GetValue(FilterProperty); }
            set { SetValue(FilterProperty, value); }
        }

        public void ChoiceFile()
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new();

            openFileDialog.Filter = Filter;
            openFileDialog.Multiselect = false;

            bool? result = openFileDialog.ShowDialog();
            if (result == true)
            {
                TxtFile.Text = openFileDialog.FileName;
                TxtFileName.Text = openFileDialog.SafeFileName;
                UploadFileName = TxtFileName.Text;
                UploadFilePath = TxtFile.Text;
                GridUpdate.Visibility = Visibility.Collapsed;
                GridShow.Visibility = Visibility.Visible;
                UploadRec.Stroke = Brushes.Gray;

                SelectChaned?.Invoke(this, new EventArgs());
            }
        }

        private void ChoiceFile_Click(object sender, RoutedEventArgs e)
        {
            ChoiceFile();
        }


        private void UIElement_OnDrop(object sender, DragEventArgs e)
        {
            var b = e.Data.GetDataPresent(DataFormats.FileDrop);

            if (b)
            {
                var sarr = e.Data.GetData(DataFormats.FileDrop);
                var a = sarr as string[];
                TxtFile.Text = a?.First();
                TxtFileName.Text = Path.GetFileName(a?.First());
                UploadFileName = TxtFileName.Text ?? string.Empty;
                UploadFilePath = TxtFile.Text ?? string.Empty;
                GridUpdate.Visibility = Visibility.Collapsed;
                GridShow.Visibility = Visibility.Visible;
                UploadRec.Stroke = Brushes.Gray;

                SelectChaned?.Invoke(this,new EventArgs());
            }


        }
    }
}
