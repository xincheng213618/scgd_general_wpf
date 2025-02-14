using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
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

namespace ColorVision.Solution.Editor
{
    /// <summary>
    /// TextEditorControl.xaml 的交互逻辑
    /// </summary>
    public partial class TextEditorControl : UserControl
    {
        public string FilePath { get; set; }
        public TextEditorControl(string filePath)
        {
            FilePath = filePath;
            InitializeComponent();
        }
        private void TreeViewControl_Drop(object sender, DragEventArgs e)
        {
            var b = e.Data.GetDataPresent(DataFormats.FileDrop);

            if (b)
            {
                var sarr = e.Data.GetData(DataFormats.FileDrop);
                var a = sarr as string[];
                var fn = a?.First();

                if (File.Exists(fn))
                {
                    if (fn.Contains(".txt|.dat"))
                    {
                        FilePath = fn;
                        OpenFile(fn);
                    }
                    else
                    {
                        MessageBox.Show("文件的格式不受支持");
                    }
                }
            }
        }
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Open, Open_Executed));
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, Save_Executed));
            OpenFile(FilePath);
        }
        private void Open_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Text documents (*.txt)|*.txt|All files (*.*)|*.*";
            if (dlg.ShowDialog() == true)
            {
                OpenFile(dlg.FileName);
            }
        }

        private void Save_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SaveFile(FilePath);
        }
        private void SaveFile(string FileName)
        {
            using FileStream fileStream = new FileStream(FileName, FileMode.OpenOrCreate);
            TextRange range = new TextRange(RichTextBox.Document.ContentStart, RichTextBox.Document.ContentEnd);
            range.Save(fileStream, DataFormats.Text);
        }

        private void OpenFile(string FilePath)
        {
            const int bufferSize = 1024; // 每次读取的字节数，可以根据需要调整
            RichTextBox.Document.Blocks.Clear(); // WPF
            TextRange textRange = new TextRange(RichTextBox.Document.ContentStart, RichTextBox.Document.ContentEnd);
            using (FileStream fileStream = new FileStream(FilePath, FileMode.Open, FileAccess.Read))
            {
                using (StreamReader reader = new StreamReader(fileStream))
                {
                    char[] buffer = new char[bufferSize];
                    int charsRead;
                    while ((charsRead = reader.Read(buffer, 0, bufferSize)) > 0)
                    {
                        string text = new string(buffer, 0, charsRead);
                        RichTextBox.AppendText(text);
                    }
                }
            }
        }
    }
}
