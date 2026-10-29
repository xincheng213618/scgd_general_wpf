#pragma warning disable CA1863
using ColorVision.UI;
using System.ComponentModel;
using System.IO;
using System.Linq;
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
            try
            {
                var existing = PhyLicenseDao.Instance.GetAll();
                var identities = DeviceLicenseImport.KnownDevices(SysResourceDao.Instance.GetAll(), existing);
                var license = DeviceLicenseImport.ReadFiles([filePath], identities, existing).Single();
                if (MessageBox.Show(string.Format(Properties.Resources.ImportLicenseConfirm, license.Model), Properties.Resources.LicenseImport, MessageBoxButton.YesNo) == MessageBoxResult.No)
                    return new FileOpenRouteResult(true, false, Canceled: true);
                PhyCameraManager.GetInstance().ImportLicenses([license]);
                return new FileOpenRouteResult(true, true);
            }
            catch (System.Exception ex) { return new FileOpenRouteResult(true, false, ex.Message); }
        }
    }
}
