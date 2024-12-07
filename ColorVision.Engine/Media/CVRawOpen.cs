﻿#pragma warning disable CS8625
using ColorVision.ImageEditor;
using ColorVision.Net;
using ColorVision.Util.Draw.Special;
using cvColorVision;
using log4net;
using MQTTMessageLib.FileServer;
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

        private static FieldInfo GetEventField(this Type type, string eventName)
        {
            FieldInfo field = null;
            while (type != null)
            {
                /* Find events defined as field */
                field = type.GetField(eventName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null && (field.FieldType == typeof(MulticastDelegate) || field.FieldType.IsSubclassOf(typeof(MulticastDelegate))))
                    break;

                /* Find events defined as property { add; remove; } */
                field = type.GetField("EVENT_" + eventName.ToUpper(), BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                    break;
                type = type.BaseType;
            }
            return field;
        }
    }

    public class CVRawOpen : IImageViewOpen
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CVRawOpen));

        public List<string> Extension { get; } = new List<string> { ".cvraw",".cvcie" };

        public List<string> ComboBoxLayerItems { get; set; } = new List<string>() { "Src", "R", "G", "B" };
        public List<string> ComboBoxLayerCIEItems { get; set; } = new List<string>() { "Src", "R", "G", "B", "X", "Y", "Z" };

        public void CVCIESetBuffer(ImageView imageView,string filePath)
        {
            
            void ShowCVCIE(object sender, ImageInfo imageInfo)
            {
                float dXVal = 0;
                float dYVal = 0;
                float dZVal = 0;
                float dx = 0, dy = 0, du = 0, dv = 0;
                _ = ConvertXYZ.CM_GetXYZxyuvRect(imageView.Config.ConvertXYZhandle, imageInfo.X, imageInfo.Y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, imageView.Config.CVCIENum, imageView.Config.CVCIENum);
                imageView.ImageEditViewMode.MouseMagnifier.DrawImageCVCIE(imageInfo, dXVal, dYVal, dZVal, dx, dy, du, dv);
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
                if (imageView.ComboBoxLayers.SelectedIndex < 0) return;

                if (ComboBoxLayerCIEItems[imageView.ComboBoxLayers.SelectedIndex] == "Src")
                {
                    imageView.OpenImage(CVFileUtil.OpenLocalFileChannel(imageView.Config.FilePath, CVImageChannelType.SRC).ToWriteableBitmap());
                }
                if (ComboBoxLayerCIEItems[imageView.ComboBoxLayers.SelectedIndex] == "R")
                {
                    imageView.OpenImage(CVFileUtil.OpenLocalFileChannel(imageView.Config.FilePath, CVImageChannelType.RGB_R).ToWriteableBitmap());
                }
                if (ComboBoxLayerCIEItems[imageView.ComboBoxLayers.SelectedIndex] == "G")
                {
                    imageView.OpenImage(CVFileUtil.OpenLocalFileChannel(imageView.Config.FilePath, CVImageChannelType.RGB_G).ToWriteableBitmap());
                }
                if (ComboBoxLayerCIEItems[imageView.ComboBoxLayers.SelectedIndex] == "B")
                {
                    imageView.OpenImage(CVFileUtil.OpenLocalFileChannel(imageView.Config.FilePath, CVImageChannelType.RGB_B).ToWriteableBitmap());
                }
                if (ComboBoxLayerCIEItems[imageView.ComboBoxLayers.SelectedIndex] == "X")
                {
                    imageView.OpenImage(CVFileUtil.OpenLocalFileChannel(imageView.Config.FilePath, CVImageChannelType.CIE_XYZ_X).ToWriteableBitmap());
                }
                if (ComboBoxLayerCIEItems[imageView.ComboBoxLayers.SelectedIndex] == "Y")
                {
                    imageView.OpenImage(CVFileUtil.OpenLocalFileChannel(imageView.Config.FilePath, CVImageChannelType.CIE_XYZ_Y).ToWriteableBitmap());
                }
                if (ComboBoxLayerCIEItems[imageView.ComboBoxLayers.SelectedIndex] == "Z")
                {
                    imageView.OpenImage(CVFileUtil.OpenLocalFileChannel(imageView.Config.FilePath, CVImageChannelType.CIE_XYZ_Z).ToWriteableBitmap());
                }
            }


            if (File.Exists(filePath) && CVFileUtil.IsCIEFile(filePath))
            {

                int index = CVFileUtil.ReadCIEFileHeader(imageView.Config.FilePath, out CVCIEFile meta);
                if (index <= 0) return;
                if (meta.FileExtType == FileExtType.CIE)
                {
                    imageView.Config.IsCVCIE = true;
                    CVFileUtil.ReadCIEFileData(imageView.Config.FilePath, ref meta, index);
                    int resultCM_SetBufferXYZ = ConvertXYZ.CM_SetBufferXYZ(imageView.Config.ConvertXYZhandle, (uint)meta.rows, (uint)meta.cols, (uint)meta.bpp, (uint)meta.channels, meta.data);
                    log.Debug($"CM_SetBufferXYZ :{resultCM_SetBufferXYZ}");
                    imageView.ImageEditViewMode.MouseMagnifier.MouseMoveColorHandler += ShowCVCIE;
                    imageView.ComboBoxLayers.ItemsSource =  ComboBoxLayerCIEItems;
                    imageView.ComboBoxLayers.SelectedIndex = 0;
                    imageView.AddSelectionChangedHandler(ComboBoxLayers1_SelectionChanged);;
                }
                else
                {
                    imageView.ComboBoxLayers.ItemsSource = ComboBoxLayerItems;
                    imageView.ComboBoxLayers.SelectedIndex = 0;
                    imageView.AddSelectionChangedHandler(ComboBoxLayers1_SelectionChanged); ;
                }
            }

        }
        public async void OpenImage(ImageView imageView, string? filePath)
        {
            CVCIESetBuffer(imageView, filePath);

            void export()
            {
                new ExportCVCIE(imageView.Config.FilePath).ShowDialog();
            }
            MenuItem menuItem = new MenuItem() { Header = "导出" };
            menuItem.Click +=(s,e) => export();
            imageView.Zoombox1.ContextMenu.Items.Add(menuItem);
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
