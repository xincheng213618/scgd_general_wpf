﻿using QRCoder;
using QRCoder.Xaml;
using System.Windows.Media;

namespace ColorVision.Engine.Utilities
{
    public class QRCodeHelper
    {
        public static DrawingImage? GetQRCode(string strContent)
        {
            try
            {
                QRCodeGenerator qrGenerator = new();
                QRCodeData qrCodeData = qrGenerator.CreateQrCode(strContent, QRCodeGenerator.ECCLevel.H);
                XamlQRCode qrCode = new(qrCodeData);
                DrawingImage qrCodeAsXaml = qrCode.GetGraphic(40);
                return qrCodeAsXaml;
            }
            catch
            {
                return null;
            }
        }
    }
}
