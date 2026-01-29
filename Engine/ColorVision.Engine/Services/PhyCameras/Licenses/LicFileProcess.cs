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

    public class LicFileProcess : IFileProcessor
    {
        public int Order => 1;

        public void Export(string filePath)
        {

        }

        public bool Process(string filePath)
        {
            if (!File.Exists(filePath)) return false;
            string content = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(content)) return false;
            string LicenseValue = Tool.Base64Decode(content);
            ColorVisionLicense colorVisionLicense =  JsonConvert.DeserializeObject<ColorVisionLicense>(LicenseValue);
            if (colorVisionLicense == null) return false;

            if (MessageBox.Show("是否导入许可证" + colorVisionLicense.DeviceMode, "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.No) return false;
            LicenseModel licenseModel = PhyLicenseDao.Instance.GetByMAC(Path.GetFileNameWithoutExtension(filePath)) ?? new LicenseModel();
            licenseModel.LicenseValue = content;
            PhyLicenseDao.Instance.Save(licenseModel);
            return true;
        }
    }
}
