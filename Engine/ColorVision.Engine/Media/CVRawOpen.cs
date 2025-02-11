#pragma warning disable CS8625
using ColorVision.Common.MVVM;
using ColorVision.ImageEditor;
using ColorVision.Net;
using ColorVision.UI.Menus;
using ColorVision.Util.Draw.Special;
using cvColorVision;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Media
{

    internal static class Extension
    {
        internal static void ClearEventInvocations(this object obj, string eventName)
        {
            var fi = obj.GetType().GetEventField(eventName);
            if (fi == null) return;
            fi.SetValue(obj, null);
        }

        private static FieldInfo? GetEventField(this Type type, string eventName)
        {
            FieldInfo field = null;
            while (type != null)
            {
                /* Find events defined as field */
                field = type.GetField(eventName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null && (field.FieldType == typeof(MulticastDelegate) || field.FieldType.IsSubclassOf(typeof(MulticastDelegate))))
                    break;

                /* Find events defined as property { add; remove; } */
                field = type.GetField("EVENT_" + eventName.ToUpper(System.Globalization.CultureInfo.CurrentCulture), BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                    break;
                type = type.BaseType;
            }
            return field;
        }
    }

    public class CVRawOpen : IImageOpen
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CVRawOpen));

        public List<string> Extension { get; } = new List<string> { ".cvraw",".cvcie" };

        public List<string> ComboBoxLayerItems { get; set; } = new List<string>() { "Src", "R", "G", "B" };
 

        public void CVCIESetBuffer(ImageView imageView,string filePath)
        {
            void ShowCVCIE(object sender, ImageInfo imageInfo)
            {
                float dXVal = 0;
                float dYVal = 0;
                float dZVal = 0;
                float dx = 0, dy = 0, du = 0, dv = 0;

                switch (imageView.Config.CVCIETYpe)
                {
                    case CVCIETYpe.Circle:
                        if (exp.Length == 1)
                        {
                            int ret = ConvertXYZ.CM_GetYCircle(imageView.Config.ConvertXYZhandle, imageInfo.X, imageInfo.Y, ref dYVal,imageView.Config.CVCIENum/2);
                            string text1 = $"Y:{dYVal:F1}";
                            string text2 = $"";
                            imageView.ImageEditViewMode.MouseMagnifier.DrawImage(imageInfo, text1, text2);
                        }
                        else
                        {

                            int ret = ConvertXYZ.CM_GetXYZxyuvCircle(imageView.Config.ConvertXYZhandle, imageInfo.X, imageInfo.Y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, imageView.Config.CVCIENum/2);
                            string text1 = $"X:{dXVal:F1},Y:{dYVal:F1},Z:{dZVal:F1}";
                            string text2 = $"x:{dx:F2},y:{dy:F2},u:{du:F2},v:{dv:F2}";
                            imageView.ImageEditViewMode.MouseMagnifier.DrawImage(imageInfo, text1, text2);
                        }

                        break;
                    case CVCIETYpe.Rect:
                        if (exp.Length == 1)
                        {
                            int ret = ConvertXYZ.CM_GetYRect(imageView.Config.ConvertXYZhandle, imageInfo.X, imageInfo.Y, ref dYVal, imageView.Config.CVCIENum, imageView.Config.CVCIENum);
                            string text1 = $"Y:{dYVal:F1}";
                            string text2 = $"";
                            imageView.ImageEditViewMode.MouseMagnifier.DrawImage(imageInfo, text1, text2);
                        }
                        else
                        {
                            int ret = ConvertXYZ.CM_GetXYZxyuvRect(imageView.Config.ConvertXYZhandle, imageInfo.X, imageInfo.Y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, imageView.Config.CVCIENum, imageView.Config.CVCIENum);
                            string text1 = $"X:{dXVal:F1},Y:{dYVal:F1},Z:{dZVal:F1}";
                            string text2 = $"x:{dx:F2},y:{dy:F2},u:{du:F2},v:{dv:F2}";
                            imageView.ImageEditViewMode.MouseMagnifier.DrawImage(imageInfo, text1, text2);
                        }

                        break;
                    default:
                        break;
                }


            }

            imageView.ImageEditViewMode.MouseMagnifier.ClearEventInvocations("MouseMoveColorHandler");

            imageView.ImageEditViewMode.ClearImageEventHandler += (s, e) =>
            {
                int result = ConvertXYZ.CM_ReleaseBuffer(imageView.Config.ConvertXYZhandle);
            };
            if (!imageView.Config.ConvertXYZhandleOnce)
            {
                int result = ConvertXYZ.CM_InitXYZ(imageView.Config.ConvertXYZhandle);
                log.Info($"ConvertXYZ.CM_InitXYZ :{result}");
                imageView.Config.ConvertXYZhandleOnce = true;
            }
            imageView.Config.FilePath = filePath;
            void ComboBoxLayers1_SelectionChanged(object sender, SelectionChangedEventArgs e)
            {
                if (sender is ComboBox comboBox)
                {
                    string layer = ComboBoxLayerItems[comboBox.SelectedIndex];
                    if (layer == "Src")
                    {
                        imageView.OpenImage(CVFileUtil.OpenLocalFileChannel(imageView.Config.FilePath, CVImageChannelType.SRC).ToWriteableBitmap());
                    }
                    if (layer == "R")
                    {
                        imageView.ExtractChannel(0);
                    }
                    if (layer == "G")
                    {
                        imageView.ExtractChannel(1);
                    }
                    if (layer == "B")
                    {
                        imageView.ExtractChannel(2);
                    }
                    if (layer == "X")
                    {
                        imageView.OpenImage(CVFileUtil.OpenLocalFileChannel(imageView.Config.FilePath, CVImageChannelType.CIE_XYZ_X).ToWriteableBitmap());
                    }
                    if (layer == "Y")
                    {
                        imageView.OpenImage(CVFileUtil.OpenLocalFileChannel(imageView.Config.FilePath, CVImageChannelType.CIE_XYZ_Y).ToWriteableBitmap());
                    }
                    if (layer == "Z")
                    {
                        imageView.OpenImage(CVFileUtil.OpenLocalFileChannel(imageView.Config.FilePath, CVImageChannelType.CIE_XYZ_Z).ToWriteableBitmap());

                    }
                }
            }


            if (File.Exists(filePath) && CVFileUtil.IsCIEFile(filePath))
            {

                int index = CVFileUtil.ReadCIEFileHeader(imageView.Config.FilePath, out CVCIEFile meta);
                if (index <= 0) return;
                if (meta.FileExtType == CVType.CIE)
                {
                    imageView.Button1931.Visibility = Visibility.Visible;

                    log.Debug(JsonConvert.SerializeObject(meta));
                    imageView.Config.AddProperties("IsCVCIE", true);
                    imageView.Config.AddProperties("Exp", meta.exp);
                    exp = meta.exp;
                    CVFileUtil.ReadCIEFileData(imageView.Config.FilePath, ref meta, index);
                    int resultCM_SetBufferXYZ = ConvertXYZ.CM_SetBufferXYZ(imageView.Config.ConvertXYZhandle, (uint)meta.rows, (uint)meta.cols, (uint)meta.bpp, (uint)meta.channels, meta.data);
                    log.Debug($"CM_SetBufferXYZ :{resultCM_SetBufferXYZ}");
                    imageView.ImageEditViewMode.MouseMagnifier.MouseMoveColorHandler += ShowCVCIE;

                    if (!File.Exists(meta.srcFileName))
                        meta.srcFileName = Path.Combine(Path.GetDirectoryName(filePath) ?? string.Empty, meta.srcFileName);

                    if (meta.channels ==3)
                    {
                        if (File.Exists(meta.srcFileName))
                        {
                            ComboBoxLayerItems = new List<string>() { "Src", "R", "G", "B", "X", "Y", "Z" };
                        }
                        else
                        {
                            ComboBoxLayerItems = new List<string>() { "Src", "X", "Y", "Z" };
                        }
                    }
                    else if (meta.channels == 1)
                    {
                        if (File.Exists(meta.srcFileName))
                        {
                            ComboBoxLayerItems = new List<string>() { "Src", "Y" };
                        }
                        else
                        {
                            ComboBoxLayerItems = new List<string>() { "Src" };
                        }
                    }
                    else
                    {
                        ComboBoxLayerItems = new List<string>() { "Src" };
                    }
                    imageView.ComboBoxLayers.ItemsSource = ComboBoxLayerItems;
                    imageView.ComboBoxLayers.SelectedIndex = 0;
                    imageView.AddSelectionChangedHandler(ComboBoxLayers1_SelectionChanged);;
                }
                else
                {
                    if (meta.channels == 3)
                    {
                        ComboBoxLayerItems = new List<string>() { "Src", "R", "G", "B" };
                    }
                    else if (meta.channels == 1)
                    {
                        ComboBoxLayerItems = new List<string>() { "Src"};
                    }
                    else
                    {
                        ComboBoxLayerItems = new List<string>() { "Src" };
                    }
                    imageView.ComboBoxLayers.ItemsSource = ComboBoxLayerItems;
                    imageView.ComboBoxLayers.SelectedIndex = 0;
                    imageView.AddSelectionChangedHandler(ComboBoxLayers1_SelectionChanged); ;
                }
            }

        }
        public float[] exp { get; set; }

        public List<MenuItemMetadata> GetContextMenuItems(ImageView imageView)
        {
            return new List<MenuItemMetadata>()
            {
                new MenuItemMetadata()
                {
                    Header = "导出",
                    GuidId = "Export",
                    Order = 150,
                    Command = new RelayCommand(a =>
                    {
                        if (imageView.Config.GetProperties<string>("FilePath") is string FilePath && File.Exists(FilePath))
                        {
                            new ExportCVCIE(FilePath).ShowDialog();
                        }
                    })
                }
            };
        }



        public async void OpenImage(ImageView imageView, string? filePath)
        {
            if (filePath == null) return;
            CVCIESetBuffer(imageView, filePath);

            try
            {
                if (imageView.Config.IsShowLoadImage)
                {
                    imageView.WaitControl.Visibility = Visibility.Visible;
                    await Task.Delay(30);
                    await Task.Run(() =>
                    {
                        CVCIEFile cVCIEFile = new NetFileUtil().OpenLocalCVFile(filePath);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            imageView.OpenImage(cVCIEFile.ToWriteableBitmap());
                            imageView.UpdateZoomAndScale();
                            imageView.WaitControl.Visibility = Visibility.Collapsed;
                        });
                    });
                }
                else
                {
                    CVCIEFile cVCIEFile = new NetFileUtil().OpenLocalCVFile(filePath);
                    imageView.OpenImage(cVCIEFile.ToWriteableBitmap());
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

        }
    }
}
