#pragma warning disable CS8625
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

        public void CVCIESetBuffer(ImageView imageView,string filePath)
        {
            void ShowCVCIE(object sender, ImageInfo imageInfo)
            {
                float dXVal = 0;
                float dYVal = 0;
                float dZVal = 0;
                float dx = 0, dy = 0, du = 0, dv = 0;
                _ = ConvertXYZ.CM_GetXYZxyuvRect(imageView.Config.ConvertXYZhandle, imageInfo.X, imageInfo.Y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, imageView.Config.CVCIENum, imageView.Config.CVCIENum);
                imageView.ToolBarTop.MouseMagnifier.DrawImageCVCIE(imageInfo, dXVal, dYVal, dZVal, dx, dy, du, dv);
            }
            imageView.ToolBarTop.MouseMagnifier.ClearEventInvocations("MouseMoveColorHandler");
            imageView.ToolBarTop.ClearImageEventHandler += (s, e) =>
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
                    imageView.ToolBarTop.MouseMagnifier.MouseMoveColorHandler += ShowCVCIE;
                }
            }

        }


        public async void OpenImage(ImageView imageView, string? filePath)
        {
            CVCIESetBuffer(imageView, filePath);
            try
            {
                if (imageView.Config.IsShowLoadImage)
                {
                    imageView.WaitControl.Visibility = Visibility.Visible;
                    await Task.Delay(100);
                    await Task.Run(() =>
                    {
                        CVCIEFile cVCIEFile = new NetFileUtil().OpenLocalCVFile(filePath);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            imageView.OpenImage(cVCIEFile.ToWriteableBitmap());

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
