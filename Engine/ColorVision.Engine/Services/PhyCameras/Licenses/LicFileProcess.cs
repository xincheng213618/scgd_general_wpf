#pragma warning disable CA1863
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.UI;
using Newtonsoft.Json;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace ColorVision.Engine.Services.PhyCameras.Licenses
{
    [FileExtension(".lic")]

    public class LicFileProcess : IFileOpenActionProcessor
    {
        public int Order => 1;

        public FileOpenRouteResult OpenFile(string filePath)
        {
            if (!File.Exists(filePath))
                return new FileOpenRouteResult(true, false, $"许可证文件不存在：{filePath}");
            string content = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(content))
                return new FileOpenRouteResult(true, false, "许可证文件为空。");
            string LicenseValue = Tool.Base64Decode(content);
            ColorVisionLicense colorVisionLicense =  JsonConvert.DeserializeObject<ColorVisionLicense>(LicenseValue);
            if (colorVisionLicense == null)
                return new FileOpenRouteResult(true, false, "许可证文件格式无效。");

            if (MessageBox.Show(string.Format(Properties.Resources.ImportLicenseConfirm, colorVisionLicense.DeviceMode), Properties.Resources.LicenseImport, MessageBoxButton.YesNo) == MessageBoxResult.No)
                return new FileOpenRouteResult(true, false, Canceled: true);
            LicenseModel licenseModel = PhyLicenseDao.Instance.GetByMAC(Path.GetFileNameWithoutExtension(filePath)) ?? new LicenseModel();
            licenseModel.LicenseValue = content;
            PhyLicenseDao.Instance.Save(licenseModel);
            return new FileOpenRouteResult(true, true);
        }
    }
}
