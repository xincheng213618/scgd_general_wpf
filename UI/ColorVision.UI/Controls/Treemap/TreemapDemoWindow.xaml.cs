using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.Controls
{
    public class MenuLogWindow : GlobalMenuBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override int Order => 10005;
        public override string Header => "Treemap Viewer";
        public override void Execute() => new TreemapDemoWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
    }

    public partial class TreemapDemoWindow : Window
    {
        // ─── Navigation stack (for drill-down / go-up) ───────────────────────

        private readonly Stack<TreemapNode> _navStack = new Stack<TreemapNode>();
        private TreemapNode? _fullRoot;   // top-level scan result

        // ─── Async scan cancellation ─────────────────────────────────────────

        private CancellationTokenSource? _cts;

        // ─── Construction / loading ──────────────────────────────────────────

        public TreemapDemoWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PopulateDrives();
            TxtProgress.Text = "请选择目录并点击"扫描"，或点击"加载"读取已保存的扫描结果。";
        }

        // ─── Drive picker ─────────────────────────────────────────────────────

        private void PopulateDrives()
        {
            CmbDrives.Items.Clear();
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.IsReady)
                        CmbDrives.Items.Add(drive);
                }
                catch { }
            }
        }

        private void CmbDrives_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbDrives.SelectedItem is DriveInfo drive)
            {
                TxtPath.Text = drive.RootDirectory.FullName;
                BtnScan.IsEnabled = true;
            }
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            // OpenFolderDialog is available in .NET 8+ WPF
            var dlg = new OpenFolderDialog
            {
                Title = "选择要扫描的目录",
                Multiselect = false
            };
            if (dlg.ShowDialog(this) == true)
            {
                TxtPath.Text = dlg.FolderName;
                BtnScan.IsEnabled = true;
                CmbDrives.SelectedIndex = -1;
            }
        }

        // ─── Real file-system scan ────────────────────────────────────────────

        private const int ProgressReportInterval = 200;

        private async void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            string path = TxtPath.Text;
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                MessageBox.Show("请先选择有效的目录。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Cancel any running scan
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            SetScanningState(true);
            TxtProgress.Text = "扫描中…";
            TxtNodeCount.Text = "—";

            try
            {
                var progress = new Progress<int>(count =>
                {
                    TxtProgress.Text = $"已扫描 {count:N0} 个文件…";
                });

                var (root, fileCount) = await Task.Run(
                    () => ScanDirectory(path, progress, ct), ct);

                if (ct.IsCancellationRequested)
                {
                    TxtProgress.Text = "已取消。";
                    return;
                }

                if (root != null)
                {
                    root.RecalculateSize();
                    _fullRoot = root;
                    _navStack.Clear();
                    SetDisplayRoot(root);
                    TxtProgress.Text = $"完成。共 {fileCount:N0} 个文件。";
                }
            }
            catch (OperationCanceledException)
            {
                TxtProgress.Text = "已取消。";
            }
            catch (Exception ex)
            {
                TxtProgress.Text = $"扫描出错：{ex.Message}";
            }
            finally
            {
                SetScanningState(false);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
        }

        /// <summary>Recursively scans a directory on a background thread.</summary>
        private static (TreemapNode? Node, int FileCount) ScanDirectory(
            string path, IProgress<int> progress, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return (null, 0);

            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(name))
                name = path; // drive root like "C:\"

            var node = new TreemapNode { Name = name, FullPath = path };
            var counter = new ScanCounter();
            ScanRecursive(node, path, progress, ct, counter);
            return (node, counter.Value);
        }

        /// <summary>Simple mutable counter used to share file count across recursive calls.</summary>
        private sealed class ScanCounter
        {
            public int Value;
        }

        private static void ScanRecursive(
            TreemapNode parent, string dirPath,
            IProgress<int> progress, CancellationToken ct, ScanCounter counter)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                foreach (string file in Directory.EnumerateFiles(dirPath))
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var fi = new FileInfo(file);
                        parent.AddChild(new TreemapNode
                        {
                            Name = fi.Name,
                            Size = fi.Length,
                            FullPath = file
                        });
                        counter.Value++;
                        if (counter.Value % ProgressReportInterval == 0)
                            progress.Report(counter.Value);
                    }
                    catch { /* skip locked/inaccessible files */ }
                }

                foreach (string dir in Directory.EnumerateDirectories(dirPath))
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var child = new TreemapNode
                        {
                            Name = Path.GetFileName(dir),
                            FullPath = dir
                        };
                        parent.AddChild(child);
                        ScanRecursive(child, dir, progress, ct, counter);
                    }
                    catch (UnauthorizedAccessException) { /* skip protected dirs */ }
                    catch (OperationCanceledException) { throw; }
                    catch { }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (OperationCanceledException) { throw; }
            catch { }
        }

        // ─── UI state helpers ─────────────────────────────────────────────────

        private void SetScanningState(bool scanning)
        {
            BtnScan.IsEnabled = !scanning;
            BtnBrowse.IsEnabled = !scanning;
            CmbDrives.IsEnabled = !scanning;
            BtnCancel.IsEnabled = scanning;
            ProgressBar.Visibility = scanning ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetDisplayRoot(TreemapNode root)
        {
            TreemapCtrl.RootNode = root;
            TxtNodeCount.Text = CountNodes(root).ToString("N0");
            BtnUp.IsEnabled = _navStack.Count > 0;
            BtnSave.IsEnabled = _fullRoot != null;
        }

        // ─── Navigation ───────────────────────────────────────────────────────

        private void BtnUp_Click(object sender, RoutedEventArgs e)
        {
            if (_navStack.Count > 0)
                SetDisplayRoot(_navStack.Pop());
        }

        // ─── Save / Load scan results ─────────────────────────────────────────

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_fullRoot == null) return;

            // Sanitise the root name to produce a safe default file name.
            char[] invalid = Path.GetInvalidFileNameChars();
            string safeName = string.Concat(
                (_fullRoot.Name ?? "scan").Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c));

            var dlg = new SaveFileDialog
            {
                Title = "保存扫描结果",
                Filter = "Treemap JSON (*.treemap.json)|*.treemap.json|所有文件|*.*",
                DefaultExt = ".treemap.json",
                FileName = $"{safeName}_scan"
            };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                _fullRoot.SaveToJson(dlg.FileName);
                TxtProgress.Text = $"已保存：{dlg.FileName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "加载扫描结果",
                Filter = "Treemap JSON (*.treemap.json)|*.treemap.json|所有文件|*.*",
                DefaultExt = ".treemap.json"
            };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                var root = TreemapNode.LoadFromJson(dlg.FileName);
                if (root == null)
                {
                    MessageBox.Show("文件格式无效或为空。", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _navStack.Clear();
                _fullRoot = root;
                SetDisplayRoot(root);
                TxtPath.Text = root.FullPath ?? root.Name;
                TxtProgress.Text = $"已加载：{dlg.FileName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─── Labels toggle ────────────────────────────────────────────────────

        private void ChkLabels_Changed(object sender, RoutedEventArgs e)
        {
            if (TreemapCtrl != null)
                TreemapCtrl.ShowLabels = ChkLabels.IsChecked == true;
        }

        // ─── Right-click context menu ─────────────────────────────────────────

        private void TreemapCtrl_NodeRightClicked(object sender, TreemapNodeEventArgs e)
        {
            var cm = new ContextMenu();
            var node = e.Node;
            bool hasPath = !string.IsNullOrEmpty(node.FullPath);
            bool isFile = hasPath && File.Exists(node.FullPath);
            bool isDir = hasPath && Directory.Exists(node.FullPath);

            // ── Open file / folder directly ──
            if (isFile)
            {
                var miOpenFile = new MenuItem
                {
                    Header = "打开文件",
                    Icon = new TextBlock { Text = "📄", FontSize = 13 }
                };
                miOpenFile.Click += (_, _) =>
                {
                    try
                    {
                        // Only open paths that still exist as files (defense-in-depth).
                        if (node.FullPath != null && File.Exists(node.FullPath))
                            System.Diagnostics.Process.Start(
                                new System.Diagnostics.ProcessStartInfo(node.FullPath)
                                { UseShellExecute = true });
                    }
                    catch { }
                };
                cm.Items.Add(miOpenFile);
            }
            if (isDir)
            {
                var miOpenDir = new MenuItem
                {
                    Header = "打开文件夹",
                    Icon = new TextBlock { Text = "📁", FontSize = 13 }
                };
                miOpenDir.Click += (_, _) =>
                {
                    try
                    {
                        // Only open paths that still exist as directories (defense-in-depth).
                        if (node.FullPath != null && Directory.Exists(node.FullPath))
                            System.Diagnostics.Process.Start(
                                new System.Diagnostics.ProcessStartInfo(node.FullPath)
                                { UseShellExecute = true });
                    }
                    catch { }
                };
                cm.Items.Add(miOpenDir);
            }

            // ── Open containing folder / directory in Explorer ──
            if (hasPath)
            {
                var miOpen = new MenuItem
                {
                    Header = isDir ? "在资源管理器中打开" : "在资源管理器中显示",
                    Icon = new TextBlock { Text = "📂", FontSize = 13 }
                };
                miOpen.Click += (_, _) =>
                {
                    try
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            UseShellExecute = true
                        };
                        if (isDir)
                        {
                            // Open the directory itself
                            psi.FileName = "explorer.exe";
                            psi.Arguments = node.FullPath!;
                        }
                        else
                        {
                            // Select the file in Explorer
                            psi.FileName = "explorer.exe";
                            psi.Arguments = $"/select,\"{node.FullPath}\"";
                        }
                        System.Diagnostics.Process.Start(psi);
                    }
                    catch { }
                };
                cm.Items.Add(miOpen);
            }

            // ── Copy path ──
            if (hasPath)
            {
                var miCopy = new MenuItem
                {
                    Header = "复制路径",
                    Icon = new TextBlock { Text = "📋", FontSize = 13 }
                };
                miCopy.Click += (_, _) =>
                {
                    try { Clipboard.SetText(node.FullPath!); } catch { }
                };
                cm.Items.Add(miCopy);
                cm.Items.Add(new Separator());
            }

            // ── Drill down (make this node the visible root) ──
            if (!node.IsLeaf)
            {
                var miDrill = new MenuItem
                {
                    Header = "向下钻取（以此为根）",
                    Icon = new TextBlock { Text = "🔍", FontSize = 13 }
                };
                miDrill.Click += (_, _) =>
                {
                    if (TreemapCtrl.RootNode != null)
                        _navStack.Push(TreemapCtrl.RootNode);
                    SetDisplayRoot(node);
                };
                cm.Items.Add(miDrill);
            }

            // ── Go up ──
            if (_navStack.Count > 0)
            {
                var miUp = new MenuItem
                {
                    Header = "返回上级",
                    Icon = new TextBlock { Text = "⬆", FontSize = 13 }
                };
                miUp.Click += (_, _) => SetDisplayRoot(_navStack.Pop());
                cm.Items.Add(miUp);
            }

            // ── Delete file ──
            if (isFile)
            {
                cm.Items.Add(new Separator());
                var miDel = new MenuItem
                {
                    Header = $"删除文件{node.Name}",
                    Icon = new TextBlock { Text = "🗑", FontSize = 13 },
                    Foreground = System.Windows.Media.Brushes.OrangeRed
                };
                miDel.Click += (_, _) =>
                {
                    var res = MessageBox.Show(
                        $"确认删除文件：\n{node.FullPath}",
                        "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (res == MessageBoxResult.Yes)
                    {
                        try
                        {
                            File.Delete(node.FullPath!);
                            // Remove node from tree and refresh
                            RemoveNodeFromParent(TreemapCtrl.RootNode, node);
                            if (TreemapCtrl.RootNode != null)
                            {
                                TreemapCtrl.RootNode.RecalculateSize();
                                SetDisplayRoot(TreemapCtrl.RootNode);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"删除失败：{ex.Message}", "错误",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                };
                cm.Items.Add(miDel);
            }

            if (cm.Items.Count == 0) return;

            cm.PlacementTarget = (UIElement)sender;
            cm.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            cm.IsOpen = true;
        }

        private void TreemapCtrl_NodeClicked(object sender, TreemapNodeEventArgs e)
        {
            // Left-click with Ctrl = drill-down shortcut
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                if (!e.Node.IsLeaf)
                {
                    if (TreemapCtrl.RootNode != null)
                        _navStack.Push(TreemapCtrl.RootNode);
                    SetDisplayRoot(e.Node);
                }
            }
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        private static bool RemoveNodeFromParent(TreemapNode? subtree, TreemapNode target)
        {
            if (subtree == null) return false;
            if (subtree.Children.Remove(target)) return true;
            foreach (var child in subtree.Children)
                if (RemoveNodeFromParent(child, target)) return true;
            return false;
        }

        private static int CountNodes(TreemapNode node)
        {
            int n = 1;
            foreach (var c in node.Children) n += CountNodes(c);
            return n;
        }
    }
}
