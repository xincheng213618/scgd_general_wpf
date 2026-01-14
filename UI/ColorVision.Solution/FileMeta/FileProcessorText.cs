using ColorVision.Solution.Editor.AvalonEditor;
using ColorVision.UI;
using System.ComponentModel;
using System.Windows;


namespace ColorVision.Solution.FileMeta
{
    [FileExtension(".dat|.ini|.txt|.cs|.json|.java|.go|.md|.py|.dat|.js|.xml|.xaml|.cpp|.c|.bat|.sql|.css|.ps1")]
    public class FileProcessorText : IFileProcessor
    {
        public int Order => 1;

        public void Export(string filePath)
        {

        }


        public bool Process(string filePath)
        {
            var control = new AvalonEditControll(filePath);
            Window window = new() { };
            if (Application.Current.MainWindow != window)
            {
                window.Owner = Application.Current.GetActiveWindow();
            }
            window.Content = control;
            window.Show();

            return true;
        }
    }



}
