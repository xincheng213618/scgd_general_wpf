namespace ColorVision.Solution.V.Folder
{
    public interface IFolder
    {
        string Name { get; set; }
        string ToolTip { get; set; }
        string Icon { get; set; }

        void Open();
        void Copy();
        void ReName();
        void Delete();
    }
}
