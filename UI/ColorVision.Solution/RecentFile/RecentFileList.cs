namespace ColorVision.Solution.RecentFile
{
    public class RecentFileList
    {
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
        public event EventHandler RecentFilesChanged;

        public List<string> RecentFiles { get => Persister.RecentFiles(MaxNumberOfFiles); } 
        public void RemoveFile(string filepath)
        {
            Persister.RemoveFile(filepath, MaxNumberOfFiles);
            RecentFilesChanged?.Invoke(this, EventArgs.Empty);
        }
        public void InsertFile(string filepath)
        {
            Persister.InsertFile(filepath, MaxNumberOfFiles);
            RecentFilesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Clear()
        {
            Persister.Clear();
            RecentFilesChanged?.Invoke(this, EventArgs.Empty);
        }




    }
}
