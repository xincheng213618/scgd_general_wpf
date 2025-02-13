using ColorVision.Common.Utilities;
using ColorVision.Solution;
using System.IO;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ColorVision.Solution.Imp.CommonText
{
    public class SolutionProcessText : ISolutionProcess
    {
        public string Name { get; set; }
        public ImageSource IconSource { get; set; }

        public Control UserControl => richTextBox;

        public string GuidId => Tool.GetMD5(FullName);

        public string FullName { get; set; }

        public void Close()
        {

        }
        private RichTextBox richTextBox = new RichTextBox() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

        public void Open()
        {
            const int bufferSize = 1024; // 每次读取的字节数，可以根据需要调整
            if (File.Exists(FullName))
            {
                TextRange textRange = new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd);
                using (FileStream fileStream = new FileStream(FullName, FileMode.Open, FileAccess.Read))
                {
                    using (StreamReader reader = new StreamReader(fileStream))
                    {
                        char[] buffer = new char[bufferSize];
                        int charsRead;
                        while ((charsRead = reader.Read(buffer, 0, bufferSize)) > 0)
                        {
                            string text = new string(buffer, 0, charsRead);
                            richTextBox.AppendText(text);
                        }
                    }
                }
            }
        }
    }

}
