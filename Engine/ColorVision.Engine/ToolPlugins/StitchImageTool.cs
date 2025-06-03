using ColorVision.Common.MVVM;
using ColorVision.UI.Extension;
using ColorVision.UI.Menus;
using OpenCvSharp;
using System;
using System.Collections.ObjectModel;
using System.Windows.Forms;

 // 现在移除这个逻辑
namespace ColorVision.Engine.ToolPlugins
{
    //public class StitchImageConfig:ViewModelBase
    //{
    //    public ObservableCollection<string> ImageFiles { get; set; } = new ObservableCollection<string>();
    //};

    //public class StitchImageTool : MenuItemBase
    //{

    //    public override string OwnerGuid => MenuItemConstants.View;
    //    public override string Header => "StitchImage";
    //    public override int Order => 100;
    //    public override void Execute()
    //    {

    //        // 创建一个OpenFileDialog实例
    //        OpenFileDialog openFileDialog = new OpenFileDialog();

    //        // 设置对话框的标题
    //        openFileDialog.Title = "选择文件";

    //        // 设置允许用户选择多个文件
    //        openFileDialog.Multiselect = true;

    //        // 设置过滤器，这里以选择所有文件为例
    //        openFileDialog.Filter = "所有文件 (*.*)|*.*";

    //        // 显示对话框
    //        DialogResult result = openFileDialog.ShowDialog();

    //        // 检查用户是否点击了“确定”按钮
    //        if (result == DialogResult.OK && openFileDialog.FileNames.Length > 0)
    //        {
    //            StitchImageConfig stitchImageConfig = new StitchImageConfig();
    //            // openFileDialog.FileNames是一个字符串数组，包含了用户选择的所有文件路径
    //            foreach (string filename in openFileDialog.FileNames)
    //            {
    //                stitchImageConfig.ImageFiles.Add(filename);
    //            }

    //            string a = stitchImageConfig.ToJsonN();
    //            OpenCVMediaHelper.M_StitchImages(a, out HImage hImage);


    //            Mat mat = Mat.FromPixelData(hImage.rows, hImage.cols, MatType.MakeType(5, hImage.channels), hImage.pData);


    //            // 创建一个SaveFileDialog实例
    //            SaveFileDialog saveFileDialog = new SaveFileDialog();

    //            // 设置对话框的标题
    //            saveFileDialog.Title = "保存图像";

    //            // 设置默认文件名
    //            saveFileDialog.FileName = "stitched_image";

    //            // 设置过滤器，这里以选择JPEG文件为例
    //            saveFileDialog.Filter = "tif 文件 (*.tif)|*.tif";

    //            // 显示对话框
    //            DialogResult saveResult = saveFileDialog.ShowDialog();

    //            // 检查用户是否点击了“保存”按钮
    //            if (saveResult == DialogResult.OK)
    //            {
    //                // 指定保存路径
    //                string savePath = saveFileDialog.FileName;

    //                // 保存Mat对象到文件
    //                if (mat.SaveImage(savePath))
    //                {
    //                    Console.WriteLine("图像保存成功！");
    //                }
    //                else
    //                {
    //                    Console.WriteLine("图像保存失败！");
    //                }
    //            }



    //        }



    //    }
    //}
}
