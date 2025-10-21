using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Templates.POI;
using ColorVision.UI;
using LiveChartsCore.SkiaSharpView.Painting.ImageFilters;
using MQTTMessageLib;
using MQTTMessageLib.FileServer;
using NPOI.SS.Formula.Functions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;


namespace ColorVision.Engine.Templates.Jsons.CompoundImg
{

    public class AlgorithmCompoundImgConfig:IConfig
    {
        public string FilePath { get; set; }

        public string FilePath1 { get; set; }
    }


    [DisplayAlgorithm(46, "图像拼接", "Json")]
    public class AlgorithmCompoundImg : DisplayAlgorithmBase
    {
        public AlgorithmCompoundImgConfig Config { get; set; } = ConfigService.Instance.GetRequiredService<AlgorithmCompoundImgConfig>();

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public RelayCommand OpenTemplateCommand { get; set; }

        public AlgorithmCompoundImg(DeviceAlgorithm deviceAlgorithm)
        {
            Device = deviceAlgorithm;
            OpenTemplateCommand = new RelayCommand(a => OpenTemplate());

            SetFilePathCommand = new RelayCommand(a => SetFilePath());
            SetFilePath1Command = new RelayCommand(a => SetFilePath1());
        }
        public int TemplateSelectedIndex { get => _TemplateSelectedIndex; set { _TemplateSelectedIndex = value; OnPropertyChanged(); } }
        private int _TemplateSelectedIndex;

        public void OpenTemplate()
        {
            new TemplateEditorWindow(new TemplateCompoundImg(), TemplateSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }

        public RelayCommand SetFilePathCommand { get; set; }
        public string FilePath { get => Config.FilePath; set { Config.FilePath = value; OnPropertyChanged(); } }

        public void SetFilePath()
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png, *.tif)|*.jpg;*.jpeg;*.png;*.tif;*.cvcie;*.cvraw|All files (*.*)|*.*";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FilePath = openFileDialog.FileName;
            }
        }
        public RelayCommand SetFilePath1Command { get; set; }

        public string FilePath1 { get => Config.FilePath1; set { Config.FilePath1 = value; OnPropertyChanged(); } }

        public void SetFilePath1()
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png, *.tif)|*.jpg;*.jpeg;*.png;*.tif;*.cvcie;*.cvraw|All files (*.*)|*.*";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FilePath1 = openFileDialog.FileName;
            }
        }




        public override UserControl GetUserControl()
        {
            UserControl ??= new DisplayCompoundImg(this);
            return UserControl;
        }
        public UserControl UserControl { get; set; }

        private string UpdateFilePath(string path1,string path2)
        {
            
            string full1 = Path.GetFullPath(path1);
            string full2 = Path.GetFullPath(path2);

            string dir1 = Path.GetDirectoryName(full1) ?? string.Empty;
            string dir2 = Path.GetDirectoryName(full2) ?? string.Empty;

            // 归一化（去掉末尾分隔符）并做不区分大小写比较（Windows 上通常不区分大小写）
            dir1 = dir1.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            dir2 = dir2.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (string.Equals(dir1, dir2, StringComparison.OrdinalIgnoreCase))
            {
                // 同一文件夹：保留第一个为完整（或相对）路径组合，第二个只保留文件名
                // 这里使用 dir + 文件名1 + ";" + 文件名2 的形式
                string folderPart = dir1.Length > 0 ? dir1 : string.Empty;
                string file1 = Path.GetFileName(full1);
                string file2 = Path.GetFileName(full2);

                if (folderPart.Length > 0)
                    return Path.Combine(folderPart, file1) + ";" + file2;
                else
                    return file1 + ";" + file2;
            }
            else
            {
                // 不同文件夹：两个都用完整路径，用分号分隔
                return full1 + ";" + full2;
            }
        }     


        public MsgRecord SendCommand(ParamBase param)
        {
            string sn = null;
            sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");

            string fileName = UpdateFilePath(FilePath,FilePath1);
            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = param.Id, Name = param.Name });

            MsgSend msg = new()
            {
                EventName = "CompoundImg",
                SerialNumber = sn,
                Params = Params
            };
            return DService.PublishAsyncClient(msg);
        }
    }
}
