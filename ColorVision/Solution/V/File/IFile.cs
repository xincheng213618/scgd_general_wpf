namespace ColorVision.Solution.V.File
{
    public interface IFile
    {
        string Name { get; set; }
        string ToolTip { get; set; }
        string Icon { get; set; }

        void Open();
        void Copy();
        void ReName();
        void Delete();
    }

    public class ImageFile : IFile
    {
        public string Name { get; set; }
        public string ToolTip { get; set; }
        public string Icon { get; set; }

        public void Copy()
        {
            throw new System.NotImplementedException();
        }

        public void Delete()
        {
            throw new System.NotImplementedException();
        }

        public void Open()
        {
            throw new System.NotImplementedException();
        }

        public void ReName()
        {
            throw new System.NotImplementedException();
        }
    }



}
