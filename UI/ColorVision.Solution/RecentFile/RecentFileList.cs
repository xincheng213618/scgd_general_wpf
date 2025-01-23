using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ColorVision.RecentFile
{
    public class RecentFileList: INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        
        
        private static List<string> RegistryKeyList = RegistryPersister.RegistryKeyList;

        public IRecentFile Persister { get; set; }

        public int MaxNumberOfFiles { get; set; }
        public int MaxPathLength { get; set; }
        /// <summary>
        /// Used in: String.Format( MenuItemFormat, index, filepath, displayPath );
        /// Default = "_{0}:  {2}"
        /// </summary>
        public string MenuItemFormatOneToNine { get; set; }

        /// <summary>
        /// Used in: String.Format( MenuItemFormat, index, filepath, displayPath );
        /// Default = "{0}:  {2}"
        /// </summary>
        public string MenuItemFormatTenPlus { get; set; }

        public RecentFileList()
        {
            Persister = new RegistryPersister();
            MaxNumberOfFiles = 100;
            MaxPathLength = 50;
            MenuItemFormatOneToNine = "_{0}:  {2}";
            MenuItemFormatTenPlus = "{0}:  {2}";
        }

        public List<string> RecentFiles { get => Persister.RecentFiles(MaxNumberOfFiles); } 
        public void RemoveFile(string filepath)
        {
            Persister.RemoveFile(filepath, MaxNumberOfFiles);
        }
        public void InsertFile(string filepath)
        {
            Persister.InsertFile(filepath, MaxNumberOfFiles);
        }

        public void Clear() => Persister.Clear(); 

        


    }
}
