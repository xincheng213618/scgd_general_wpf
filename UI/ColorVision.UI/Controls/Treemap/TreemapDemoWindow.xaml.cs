using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.UI.Controls
{
    public class MenuLogWindow : GlobalMenuBase
    {
        public override string OwnerGuid => MenuItemConstants.Help;
        public override int Order => 10005;
        public override string Header => "TreemapDemoWindow";
        public override void Execute() => new TreemapDemoWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
    }


    public partial class TreemapDemoWindow : Window
    {
        public TreemapDemoWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => LoadMockData();
        }

        private void BtnMockData_Click(object sender, RoutedEventArgs e) => LoadMockData();

        private void ChkLabels_Changed(object sender, RoutedEventArgs e)
        {
            if (TreemapCtrl != null)
                TreemapCtrl.ShowLabels = ChkLabels.IsChecked == true;
        }

        private void LoadMockData()
        {
            var root = BuildMockFileSystem();
            root.RecalculateSize();
            TreemapCtrl.RootNode = root;
            TxtNodeCount.Text = CountNodes(root).ToString();
        }

        private static int CountNodes(TreemapNode node)
        {
            int n = 1;
            foreach (var c in node.Children) n += CountNodes(c);
            return n;
        }

        /// <summary>Builds a realistic-looking mock file system hierarchy (sizes in bytes).</summary>
        private static TreemapNode BuildMockFileSystem()
        {
            var root = new TreemapNode { Name = "C:\\" };

            root.AddChild(BuildDir("Windows",
                BuildDir("System32",
                    Leaf("ntoskrnl.exe", 12_000_000),
                    Leaf("ntdll.dll", 2_100_000),
                    Leaf("kernel32.dll", 1_200_000),
                    Leaf("user32.dll", 980_000),
                    Leaf("gdi32.dll", 650_000),
                    BuildDir("drivers",
                        Leaf("tcpip.sys", 2_400_000),
                        Leaf("ntfs.sys", 1_800_000),
                        Leaf("dxgkrnl.sys", 3_200_000),
                        Leaf("storport.sys", 860_000),
                        Leaf("acpi.sys", 430_000)
                    )
                ),
                BuildDir("SysWOW64",
                    Leaf("kernel32.dll", 950_000),
                    Leaf("user32.dll", 820_000),
                    Leaf("ntdll.dll", 1_900_000)
                ),
                BuildDir("WinSxS",
                    Leaf("manifest-files.bin", 45_000_000),
                    Leaf("assemblies.bin", 120_000_000)
                ),
                BuildDir("Temp",
                    Leaf("tmp001.tmp", 5_000_000),
                    Leaf("tmp002.tmp", 3_400_000),
                    Leaf("setup.log", 1_200_000)
                )
            ));

            root.AddChild(BuildDir("Program Files",
                BuildDir("Microsoft Office",
                    BuildDir("root",
                        BuildDir("Office16",
                            Leaf("WINWORD.EXE", 31_000_000),
                            Leaf("EXCEL.EXE", 34_000_000),
                            Leaf("POWERPNT.EXE", 28_000_000),
                            Leaf("OUTLOOK.EXE", 29_000_000)
                        )
                    )
                ),
                BuildDir("Google",
                    BuildDir("Chrome",
                        BuildDir("Application",
                            Leaf("chrome.exe", 3_200_000),
                            BuildDir("126.0.6478.63",
                                Leaf("chrome.dll", 185_000_000),
                                Leaf("resources.pak", 8_200_000),
                                Leaf("icudtl.dat", 10_500_000)
                            )
                        )
                    )
                ),
                BuildDir("dotnet",
                    BuildDir("shared",
                        BuildDir("Microsoft.NETCore.App",
                            BuildDir("8.0.0",
                                Leaf("coreclr.dll", 6_800_000),
                                Leaf("clrjit.dll", 5_200_000),
                                Leaf("System.Private.CoreLib.dll", 9_700_000)
                            )
                        )
                    )
                )
            ));

            root.AddChild(BuildDir("Users",
                BuildDir("Public",
                    BuildDir("Documents", Leaf("readme.txt", 12_000)),
                    BuildDir("Downloads")
                ),
                BuildDir("Developer",
                    BuildDir("Documents",
                        BuildDir("projects",
                            BuildDir("my-app",
                                Leaf("README.md", 8_000),
                                BuildDir("src",
                                    Leaf("main.cs", 45_000),
                                    Leaf("helpers.cs", 23_000),
                                    Leaf("models.cs", 31_000)
                                ),
                                BuildDir("bin",
                                    Leaf("my-app.exe", 2_400_000),
                                    Leaf("my-app.dll", 1_800_000)
                                )
                            )
                        )
                    ),
                    BuildDir("Videos",
                        Leaf("holiday-2024.mp4", 4_200_000_000),
                        Leaf("project-demo.mkv", 1_800_000_000),
                        Leaf("screencast.mov", 980_000_000)
                    ),
                    BuildDir("Pictures",
                        Leaf("IMG_0001.jpg", 6_200_000),
                        Leaf("IMG_0002.jpg", 5_800_000),
                        Leaf("IMG_0003.jpg", 7_100_000),
                        Leaf("wallpaper.png", 3_400_000)
                    ),
                    BuildDir("Downloads",
                        Leaf("vs_community.exe", 1_800_000_000),
                        Leaf("ubuntu-22.04.iso", 1_200_000_000),
                        Leaf("node-v20.iso", 28_000_000)
                    )
                )
            ));

            root.AddChild(new TreemapNode { Name = "pagefile.sys", Size = 8_589_934_592 });

            return root;
        }

        private static TreemapNode BuildDir(string name, params TreemapNode[] children)
        {
            var dir = new TreemapNode { Name = name };
            foreach (var c in children)
                dir.AddChild(c);
            return dir;
        }

        private static TreemapNode Leaf(string name, double size) =>
            new TreemapNode { Name = name, Size = size };
    }
}
