using ColorVision.Common.Extension;
using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Solution;
using ColorVision.Themes;
using cvColorVision;
using System;
using System.Collections.Generic;
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

            ComboxCameraType.ItemsSource = from e1 in Enum.GetValues(typeof(CameraType)).Cast<CameraType>()
                                           select new KeyValuePair<CameraType, string>(e1, e1.ToDescription());

            ComboxCameraTakeImageMode.ItemsSource = from e1 in Enum.GetValues(typeof(TakeImageMode)).Cast<TakeImageMode>()
                                                    select new KeyValuePair<TakeImageMode, string>(e1, e1.ToDescription());

            ComboxCameraImageBpp.ItemsSource = from e1 in Enum.GetValues(typeof(ImageBpp)).Cast<ImageBpp>()
                                               select new KeyValuePair<ImageBpp, string>(e1, e1.ToDescription());


            CameraID.ItemsSource = SysResourceDao.Instance.GetAllCameraId();

            var type = EditConfig.CameraType;

            if (type == CameraType.LV_Q || type == CameraType.LV_H || type == CameraType.LV_MIL_CL || type == CameraType.MIL_CL)
            {
                ComboxCameraChannel.ItemsSource = from e1 in Enum.GetValues(typeof(ImageChannel)).Cast<ImageChannel>()
                                                  where e1 != ImageChannel.Three
                                                  select new KeyValuePair<ImageChannel, string>(e1, e1.ToDescription());
            }
            else if (type == CameraType.CV_Q || type == CameraType.BV_Q || type == CameraType.BV_H)
            {
                ComboxCameraChannel.ItemsSource = from e1 in Enum.GetValues(typeof(ImageChannel)).Cast<ImageChannel>()
                                                  where e1 != ImageChannel.One
                                                  select new KeyValuePair<ImageChannel, string>(e1, e1.ToDescription());
            }
            else
            {
                ComboxCameraChannel.ItemsSource = from e1 in Enum.GetValues(typeof(ImageChannel)).Cast<ImageChannel>()
                                                  select new KeyValuePair<ImageChannel, string>(e1, e1.ToDescription());


            };


            ComboxCameraType.SelectionChanged += (s, e) =>
            {
                if (ComboxCameraType.SelectedValue is CameraType type)
                {
                    if (type == CameraType.LV_Q || type == CameraType.LV_H || type == CameraType.LV_MIL_CL || type == CameraType.MIL_CL)
                    {
                        ComboxCameraChannel.ItemsSource = from e1 in Enum.GetValues(typeof(ImageChannel)).Cast<ImageChannel>()
                                                          where e1 != ImageChannel.Three
                                                          select new KeyValuePair<ImageChannel, string>(e1, e1.ToDescription());
                        ComboxCameraChannel.SelectedValue = ImageChannel.One;
                    }
                    else if (type == CameraType.CV_Q || type == CameraType.BV_Q || type == CameraType.BV_H)
                    {
                        ComboxCameraChannel.ItemsSource = from e1 in Enum.GetValues(typeof(ImageChannel)).Cast<ImageChannel>()
                                                          where e1 != ImageChannel.One
                                                          select new KeyValuePair<ImageChannel, string>(e1, e1.ToDescription());
                        ComboxCameraChannel.SelectedValue = ImageChannel.Three;
                    }

                    else
                    {
                        ComboxCameraChannel.ItemsSource = from e1 in Enum.GetValues(typeof(ImageChannel)).Cast<ImageChannel>()
                                                          select new KeyValuePair<ImageChannel, string>(e1, e1.ToDescription());
                    };
                }

            };

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


            EditConfig.CopyTo(PhyCamera.Config);
            Close();
        }

        private void FileBasePath_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new();
            dialog.UseDescriptionForTitle = true;
            dialog.Description = "为相机路径选择位置";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (string.IsNullOrEmpty(dialog.SelectedPath))
                {
                    MessageBox.Show("文件夹路径不能为空", "提示");
                    return;
                }
                EditConfig.FileServerCfg.FileBasePath = dialog.SelectedPath;
            }
        }
    }
}
