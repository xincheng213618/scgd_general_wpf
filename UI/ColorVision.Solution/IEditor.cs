using System.Windows.Controls;

namespace ColorVision.Solution
{
    public interface IEditor
    {
        string Extension { get; }
        Control Open(string FilePath);
        void Close();
    }

    public class IEditorBase: IEditor
    {
        public string Extension { get; set; }

        public Control Open(string FilePath)
        {
            return new TextBox();
        }
        public void Close()
        {
        }
    }
}
