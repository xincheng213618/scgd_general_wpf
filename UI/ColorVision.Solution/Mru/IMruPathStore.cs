namespace ColorVision.Solution.Mru
{
    internal interface IMruPathStore
    {
        IReadOnlyList<MruPathEntry> Load();
        void Save(IReadOnlyList<MruPathEntry> entries);
    }
}
