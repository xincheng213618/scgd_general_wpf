using ColorVision.Solution;
using ColorVision.Solution.Editor;
using ColorVision.UI;
using ColorVision.UI.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Interop;

namespace ColorVision
{

    public partial class MainWindow
    {

        const uint WM_USER = 0x0400; // 用户自定义消息起始值

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int GlobalGetAtomName(ushort nAtom, char[] retVal, int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern ushort GlobalDeleteAtom(ushort nAtom);


        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr handle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(handle).AddHook(new HwndSourceHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) => {
                if (msg == WM_USER + 1)
                {
                    try
                    {
                        char[] chars = new char[1024];
                        int size = GlobalGetAtomName((ushort)lParam, chars, chars.Length);
                        if (size > 0)
                        {
                            string receivedString = new(chars, 0, size);
                            GlobalDeleteAtom((ushort)lParam);

                            char separator = '\u0001';
                            string[] parsedArgs = receivedString.Split(separator);
                            var parser = ArgumentParser.GetInstance();
                            IReadOnlyDictionary<string, string> parsedValues = parser.ParseValues(parsedArgs);
                            parsedValues.TryGetValue("input", out string? inputFile);
                            parsedValues.TryGetValue("solutionpath", out string? solutionPath);
                            _ = OpenReceivedResourcesAsync(inputFile, solutionPath);
                            parsedValues.TryGetValue("project", out string? project);

                            List<IFeatureLauncher> IProjects = new List<IFeatureLauncher>();
                            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
                            {
                                foreach (Type type in assembly.GetTypes().Where(t => typeof(IFeatureLauncher).IsAssignableFrom(t) && !t.IsAbstract))
                                {
                                    if (Activator.CreateInstance(type) is IFeatureLauncher projects)
                                    {
                                        IProjects.Add(projects);
                                    }
                                }
                            }
                            if (IProjects.Find(a => a.Header == project) is IFeatureLauncher project1)
                            {
                                project1.Execute();
                            }
                        }
                    }
                    catch (Exception ex) 
                    { 
                        log.Error(ex);
                    }
                }
                return IntPtr.Zero;
            }));
        }

        private static async Task OpenReceivedResourcesAsync(
            string? inputPath,
            string? solutionPath)
        {
            if (!string.IsNullOrWhiteSpace(solutionPath))
                await ResourceOpenService.Instance.TryOpenAsync(solutionPath);
            if (!string.IsNullOrWhiteSpace(inputPath)
                && !string.Equals(inputPath, solutionPath, StringComparison.OrdinalIgnoreCase))
            {
                await ResourceOpenService.Instance.TryOpenAsync(inputPath);
            }
        }
    }
}
