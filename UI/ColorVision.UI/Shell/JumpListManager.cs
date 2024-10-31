using System.Windows;
using System.Windows.Shell;

namespace ColorVision.UI.Shell
{
    public class JumpListManager
    {
        private static JumpListManager _instance;
        private static readonly object _locker = new();
        public static JumpListManager GetInstance() { lock (_locker) { return _instance ??= new JumpListManager(); } }

        private readonly JumpList _jumpList;
        private readonly string? _applicationPath;

        public JumpListManager()
        {
            _jumpList = new JumpList();
            _jumpList.ShowRecentCategory = true;
            _jumpList.ShowFrequentCategory = true;
            JumpList.SetJumpList(Application.Current, _jumpList);
            _applicationPath = System.Reflection.Assembly.GetEntryAssembly()?.Location.Replace(".dll", ".exe");
        }

        public void AddRecentFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            var jumpTask = new JumpTask
            {
                ApplicationPath = _applicationPath,
                Arguments = $"-s \"{filePath}\"",
                Title = System.IO.Path.GetFileName(filePath),
                Description = filePath,
                CustomCategory = "Recent Projects",
            };

            _jumpList.JumpItems.Add(jumpTask);
            _jumpList.Apply();
        }

        public void AddRecentFiles(IEnumerable<string> filePaths)
        {
            foreach (var filePath in filePaths)
            {
                AddRecentFile(filePath);
            }
        }

        public void ClearRecentFiles()
        {
            _jumpList.JumpItems.Clear();
            _jumpList.Apply();
        }
    }
}
