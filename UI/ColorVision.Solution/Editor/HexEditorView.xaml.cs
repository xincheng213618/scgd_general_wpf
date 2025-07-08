using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WpfHexaEditor.Core;

namespace ColorVision.Solution.Editor
{
    /// <summary>
    /// HexEditorView.xaml 的交互逻辑
    /// </summary>
    public partial class HexEditorView : UserControl,IDisposable
    {
        public HexEditorView()
        {
            InitializeComponent();
        }

        public void Dispose()
        {
            HexEditor.Dispose();
            GC.SuppressFinalize(this);
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            HexEditor.PreloadByteInEditorMode = PreloadByteInEditor.MaxVisibleLineExtended;
            AllowDrop = true;
            Drop += HexEditorView_Drop;
        }

        private void HexEditorView_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0)
                {
                    try
                    {
                        HexEditor.FileName = files[0];
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"加载文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}
