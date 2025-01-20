using ColorVision.Solution;
using ColorVision.UI;
using ColorVision.UI.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ColorVision
{

    public partial class MainWindow
    {

        const uint WM_USER = 0x0400; // 用户自定义消息起始值

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int GlobalGetAtomName(ushort nAtom, char[] retVal, int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern short GlobalDeleteAtom(short nAtom);


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
                            GlobalDeleteAtom((short)wParam);

                            char separator = '\u0001';
                            string[] parsedArgs = receivedString.Split(separator);
                            var parser = ArgumentParser.GetInstance();
                            parser.Parse(parsedArgs);
                            string inputFile = parser.GetValue("input");
                            if (inputFile != null)
                            {
                                FileProcessorFactory.GetInstance().HandleFile(inputFile);
                            }
                            string s = parser.GetValue("solutionpath");
                            if (s != null)
                            {
                                SolutionManager.GetInstance().OpenSolution(s);
                            }
                            string project = parser.GetValue("project");

                            List<IProject> IProjects = new List<IProject>();
                            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
                            {
                                foreach (Type type in assembly.GetTypes().Where(t => typeof(IProject).IsAssignableFrom(t) && !t.IsAbstract))
                                {
                                    if (Activator.CreateInstance(type) is IProject projects)
                                    {
                                        IProjects.Add(projects);
                                    }
                                }
                            }
                            if (IProjects.Find(a => a.Header == project) is IProject project1)
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
    }
}
