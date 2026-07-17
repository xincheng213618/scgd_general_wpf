using System.Windows;
using System.Windows.Shell;

namespace ColorVision.UI.Shell
{
    public sealed class JumpListManager
    {
        private const int ItemLimit = 10;
        private readonly JumpList _jumpList;
        private readonly string? _applicationPath;

        public JumpListManager()
        {
            _jumpList = new JumpList
            {
                ShowRecentCategory = false,
                ShowFrequentCategory = false,
            };
            JumpList.SetJumpList(Application.Current, _jumpList);
            _applicationPath = Environment.ProcessPath;
        }

        public void SetRecentWorkspaces(IEnumerable<string> paths)
        {
            ArgumentNullException.ThrowIfNull(paths);
            _jumpList.JumpItems.Clear();
            foreach (string path in paths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(ItemLimit))
            {
                string title = System.IO.Path.GetFileName(
                    System.IO.Path.TrimEndingDirectorySeparator(path));
                _jumpList.JumpItems.Add(new JumpTask
                {
                    ApplicationPath = _applicationPath,
                    Arguments = $"-s \"{path}\"",
                    Title = string.IsNullOrWhiteSpace(title) ? path : title,
                    Description = path,
                    CustomCategory = "Recent Workspaces",
                });
            }
            _jumpList.Apply();
        }
    }
}
