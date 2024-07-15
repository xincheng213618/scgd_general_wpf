using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Resources;

namespace ColorVision.Engine.Utilities
{
    public static class SoundPlayerHelper
    {
        public static void PlayEmbeddedResource(string resourcePath)
        {
            try
            {
                // 获取嵌入资源的流
                Uri resourceUri = new Uri(resourcePath, UriKind.Relative);
                StreamResourceInfo resourceInfo = Application.GetResourceStream(resourceUri);

                if (resourceInfo != null)
                {
                    using (Stream stream = resourceInfo.Stream)
                    {
                        SoundPlayer player = new SoundPlayer(stream);
                        player.Play();
                    }
                }
                else
                {
                    MessageBox.Show("Resource not found.");
                }
            }
            catch (Exception ex)
            {
                // 处理异常
                MessageBox.Show($"Error playing sound: {ex.Message}");
            }
        }
    }
}
