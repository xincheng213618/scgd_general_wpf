using ColorVision.Solution.Properties;
using ColorVision.Solution.V.Files;
using ColorVision.Solution.V.Folders;
using ColorVision.UI;
using log4net;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;

namespace ColorVision.Solution.V
{
    public class VMUtil
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(VMUtil));
        public static VMUtil Instance { get; set; } = new VMUtil();

        public VMUtil()
        {
            GeneraFileTypes();
        }

        public static void CreatFolders(VObject vObject,string FullName)
        {
            try
            {
                string name = Path.Combine(FullName, Resources.NewFolder);
                int count = 2;
                string newName = name;
                while (Directory.Exists(newName))
                {
                    newName = Path.Combine(FullName, Resources.NewFolder  +"("+ count +")");
                    count++;
                }
                Directory.CreateDirectory(newName);
                DirectoryInfo directoryInfo = new DirectoryInfo(newName);
                VMUtil.Instance.ManagerObject.Add(directoryInfo.FullName);
                VFolder vFolder = new VFolder(new BaseFolder(directoryInfo));
                vObject.AddChild(vFolder);
                vObject.SortByName();
                if (!vObject.IsExpanded)
                    vObject.IsExpanded = true;
                vFolder.IsExpanded = true;
                vFolder.IsEditMode = true;
                vFolder.IsSelected = true;

            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public List<string> ManagerObject { get; set; } = new List<string>();

        public async Task GeneralChild(VObject vObject, DirectoryInfo directoryInfo)
        {
            foreach (var item in directoryInfo.GetDirectories())
            {
                if ((item.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                {
                    continue;
                }
                BaseFolder folder = new(item);
                var vFolder = new VFolder(folder);
                vObject.AddChild(vFolder);
                await GeneralChild(vFolder, item);
            }

            foreach (var item in directoryInfo.GetFiles())
            {
                i++;
                if (i == 10)
                {
                    await Task.Delay(100);
                    i = 0;
                }
                var _stopwatch = Stopwatch.StartNew();
                CreateFile(vObject, item);
                _stopwatch.Stop();
                log.Debug($"{item.FullName}加载时间: {_stopwatch.Elapsed.TotalSeconds} 秒");
            }
        }

        int i;
        public async Task CreateDir(VObject vObject, DirectoryInfo directoryInfo)
        {
            if (VMUtil.Instance.ManagerObject.Contains(directoryInfo.FullName))
                return;
            VMUtil.Instance.ManagerObject.Add(directoryInfo.FullName);
            BaseFolder folder = new(directoryInfo);
            var vFolder = new VFolder(folder);
            vObject.AddChild(vFolder);
            await GeneralChild(vFolder, directoryInfo);

            foreach (var item in directoryInfo.GetFiles())
            {
                i++;
                if (i == 100)
                {
                    await Task.Delay(100);
                    i = 0;
                }
                CreateFile(vObject, item);
            }
        }


        public Dictionary<string, Type> FileTypes { get; set; }

        public void GeneraFileTypes()
        {
            FileTypes = new Dictionary<string, Type>();
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(IFileMeta).IsAssignableFrom(type) && !type.IsInterface)
                    {
                        if (Activator.CreateInstance(type) is IFileMeta page)
                        {
                            FileTypes.Add(page.Extension, type);
                        }
                    }
                }
            }
        }

        private static Regex WildcardToRegex(string pattern)
        {
            return new Regex("^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase);
        }


        public void CreateFile(VObject vObject, FileInfo fileInfo)
        {
            if (fileInfo.Extension.Contains("cvsln")) return;
            if (VMUtil.Instance.ManagerObject.Contains(fileInfo.FullName))
            {
                return;
            }
            VMUtil.Instance.ManagerObject.Add(fileInfo.FullName);

            string extension = fileInfo.Extension;
            if (fileInfo.Extension.Contains("lnk"))
            {
                string targetPath = Common.NativeMethods.ShortcutCreator.GetShortcutTargetFile(fileInfo.FullName);
                extension = Path.GetExtension(targetPath);
                fileInfo = new FileInfo(targetPath);
            }
            List<Type> matchingTypes = new List<Type>();
            if (FileTypes.TryGetValue(extension, out Type specificTypes))
            {
                matchingTypes.Add(specificTypes);
            }
            foreach (var key in FileTypes.Keys)
            {
                if (key.Contains(extension))
                    matchingTypes.Add(FileTypes[key]);
            }
            foreach (var key in FileTypes.Keys)
            {
                var subKeys = key.Split('|');
                foreach (var subKey in subKeys)
                {
                    if (WildcardToRegex(subKey).IsMatch(extension))
                    {
                        matchingTypes.Add(FileTypes[key]);
                        break;
                    }
                }
            }
            if (matchingTypes.Count > 0)
            {
                if (Activator.CreateInstance(matchingTypes[0], fileInfo) is IFileMeta file)
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        VFile vFile = new VFile(file);
                        vObject.AddChild(vFile);
                    });
                }

            }

        }



    }
}
