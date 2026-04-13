namespace WindowsServicePlugin.ServiceManager
{
    public class ServicePackageInfo
    {
        public Version Version { get; }
        public string FileName { get; }
        public string DownloadUrl { get; }

        public ServicePackageInfo(Version version, string fileName, string downloadUrl)
        {
            Version = version;
            FileName = fileName;
            DownloadUrl = downloadUrl;
        }
    }
}
