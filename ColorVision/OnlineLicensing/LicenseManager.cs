using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.OnlineLicensing
{
    public class LicenseManager
    {
        private static LicenseManager _instance;
        private static readonly object _locker = new();
        public static LicenseManager GetInstance() { lock (_locker) { return _instance ??= new LicenseManager(); } }
        public ObservableCollection<LicenseConfig> Licenses { get; set; }

        public LicenseManager()
        {
            Licenses = new ObservableCollection<LicenseConfig>();
            Licenses.Add(new LicenseConfig() { Name = "ColorVision", Sn = "0000005EAD286752E9BF44AD08D23250", Tag = $"免费版\n\r永久有效", IsCanImport = false });
        }

        public void AddLicense(LicenseConfig licenseConfig)
        {
            Application.Current.Dispatcher.Invoke(() => Licenses.Add(licenseConfig));
        }
    }
}
