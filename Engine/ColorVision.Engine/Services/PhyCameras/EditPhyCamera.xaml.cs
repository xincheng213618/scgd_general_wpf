using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using cvColorVision;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


namespace ColorVision.Engine.Services.PhyCameras
{
    /// <summary>
    /// EditPhyCamera.xaml 的交互逻辑
    /// </summary>
    public partial class EditPhyCamera : Window
    {
        public PhyCamera PhyCamera { get; set; }

        public ConfigPhyCamera EditConfig { get; set; }

        public EditPhyCamera(PhyCamera phyCamera)
        {
            PhyCamera = phyCamera;
            InitializeComponent();
            this.ApplyCaption();
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Common.NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = PhyCamera;

            EditConfig = PhyCamera.Config.Clone();
            EditContent.DataContext = EditConfig;


            ComboxCameraModel.ItemsSource = from e1 in Enum.GetValues<CameraModel>().Cast<CameraModel>()
                                            select new KeyValuePair<CameraModel, string>(e1, e1.ToDescription());

            ComboxCameraMode.ItemsSource = from e1 in Enum.GetValues<CameraMode>().Cast<CameraMode>()
                                           select new KeyValuePair<CameraMode, string>(e1, e1.ToDescription());

            ComboxCameraTakeImageMode.ItemsSource = from e1 in Enum.GetValues<TakeImageMode>().Cast<TakeImageMode>()
                                                    select new KeyValuePair<TakeImageMode, string>(e1, e1.ToDescription());

            ComboxCameraImageBpp.ItemsSource = from e1 in Enum.GetValues<ImageBpp>().Cast<ImageBpp>()
                                               select new KeyValuePair<ImageBpp, string>(e1, e1.ToDescription());
            ComboxCameraChannel.ItemsSource = from e1 in Enum.GetValues<ImageChannel>().Cast<ImageChannel>()
                                              select new KeyValuePair<ImageChannel, string>(e1, e1.ToDescription());

            ComboxCameraChannel.ItemsSource = from e1 in Enum.GetValues<ImageChannel>().Cast<ImageChannel>()
                                              select new KeyValuePair<ImageChannel, string>(e1, e1.ToDescription());

            var ImageChannelTypeList = new[]{
                 new KeyValuePair<ImageChannelType, string>(ImageChannelType.Gray_X, "Channel_R"),
                 new KeyValuePair<ImageChannelType, string>(ImageChannelType.Gray_Y, "Channel_G"),
                 new KeyValuePair<ImageChannelType, string>(ImageChannelType.Gray_Z, "Channel_B")
            };
            chType1.ItemsSource = ImageChannelTypeList;
            chType2.ItemsSource = ImageChannelTypeList;
            chType3.ItemsSource = ImageChannelTypeList;


            Dictionary<ImageChannelType, ComboBox> keyValuePairs = new()
            {
                { ImageChannelType.Gray_X, chType1 },
                { ImageChannelType.Gray_Y, chType2 },
                { ImageChannelType.Gray_Z, chType3 }
            };

            if (EditConfig.CFW.ChannelCfgs.Count == 0)
            {
                EditConfig.CFW.ChannelCfgs.Add(new() { Cfwport = 0, Chtype = ImageChannelType.Gray_Y });
                EditConfig.CFW.ChannelCfgs.Add(new() { Cfwport = 1, Chtype = ImageChannelType.Gray_X });
                EditConfig.CFW.ChannelCfgs.Add(new() { Cfwport = 2, Chtype = ImageChannelType.Gray_Z });
            }
            while (EditConfig.CFW.ChannelCfgs.Count< 9)
            {
                EditConfig.CFW.ChannelCfgs.Add(new Services.PhyCameras.Configs.ChannelCfg());
            }

            StackPanelInfo.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(EditConfig));
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (EditConfig.CFW.CFWNum > 1)
            {
                EditConfig.CFW.ChannelCfgs[3].Chtype = EditConfig.CFW.ChannelCfgs[0].Chtype;
                EditConfig.CFW.ChannelCfgs[4].Chtype = EditConfig.CFW.ChannelCfgs[1].Chtype;
                EditConfig.CFW.ChannelCfgs[5].Chtype = EditConfig.CFW.ChannelCfgs[2].Chtype;
            }
            if (EditConfig.CFW.CFWNum > 2)
            {
                EditConfig.CFW.ChannelCfgs[6].Chtype = EditConfig.CFW.ChannelCfgs[0].Chtype;
                EditConfig.CFW.ChannelCfgs[7].Chtype = EditConfig.CFW.ChannelCfgs[1].Chtype;
                EditConfig.CFW.ChannelCfgs[8].Chtype = EditConfig.CFW.ChannelCfgs[2].Chtype;
            }
            if (EditConfig.CFW.CFWNum ==1)
                EditConfig.CFW.ChannelCfgs = EditConfig.CFW.ChannelCfgs.GetRange(0, 3);
            if (EditConfig.CFW.CFWNum == 2)
                EditConfig.CFW.ChannelCfgs = EditConfig.CFW.ChannelCfgs.GetRange(0, 6);
            if (EditConfig.CFW.CFWNum == 3)
                EditConfig.CFW.ChannelCfgs = EditConfig.CFW.ChannelCfgs.GetRange(0, 9);


            if (!string.Equals(EditConfig.FileServerCfg.FileBasePath, PhyCamera.Config.FileServerCfg.FileBasePath, StringComparison.Ordinal))
            {
                MessageBox1.Show("您需要手动重启服务，并且将原来文件夹复制到新的文件夹里，否则不起效果，如果未复制文件，请重置校正文件");
                string sourceDir = PhyCamera.Config.FileServerCfg.FileBasePath + "\\" + PhyCamera.Code;
                string targetDir = EditConfig.FileServerCfg.FileBasePath + "\\" + PhyCamera.Code;
                if (!Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);
                targetDir = EditConfig.FileServerCfg.FileBasePath;
                if (MessageBox1.Show($"自动复制文件夹 {sourceDir} to {targetDir}  ", "ColorVision",MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    if (!Directory.Exists(targetDir))
                        Directory.CreateDirectory(targetDir);
                    try
                    {
                        Common.NativeMethods.ShellFileOperations.Move(sourceDir, targetDir);
                        MessageBox.Show("文件夹复制成功！");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("文件夹复制失败: " + ex.Message);
                    }

                }

            }
            EditConfig.CopyTo(PhyCamera.Config);
            Close();
        }
    }
}
