using ColorVision.Solution.Properties;
using log4net;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ColorVision.Solution.V
{

    public class VMUtil
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(VMUtil));
        public static VMUtil Instance { get; set; } = new VMUtil();

        public VMUtil()
        {
            // Initialize both registries
            VObjectFactory.InitializeRegistries();
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
                
                // Use factory pattern with new registry system
                VFolder vFolder = VObjectFactory.CreateVFolder(directoryInfo);
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

        public async Task GeneralChild(IObject vObject, DirectoryInfo directoryInfo)
        {
            foreach (var item in directoryInfo.GetDirectories())
            {
                if ((item.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                {
                    continue;
                }
                // Use factory pattern with new registry system
                var vFolder = VObjectFactory.CreateVFolder(item);
                vObject.AddChild(vFolder);
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
        public async Task CreateDir(IObject vObject, DirectoryInfo directoryInfo)
        {
            // Use factory pattern with new registry system
            var vFolder = VObjectFactory.CreateVFolder(directoryInfo);
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

        public void CreateFile(IObject vObject, FileInfo fileInfo)
        {
            if (fileInfo.Extension.Contains("cvsln")) return;

            string extension = fileInfo.Extension;
            if (fileInfo.Extension.Contains("lnk"))
            {
                string targetPath = Common.NativeMethods.ShortcutCreator.GetShortcutTargetFile(fileInfo.FullName);
                extension = Path.GetExtension(targetPath);
                fileInfo = new FileInfo(targetPath);
            }
            
            // Use factory pattern for consistent VFile creation
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                VFile vFile = VObjectFactory.CreateVFile(fileInfo);
                vObject.AddChild(vFile);
            });
        }



    }
}
