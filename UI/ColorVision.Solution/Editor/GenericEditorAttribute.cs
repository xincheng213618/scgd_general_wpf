namespace ColorVision.Solution
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class GenericEditorAttribute : Attribute
    {
        public string? Name { get; }
        public GenericEditorAttribute(string? name = null)
        {
            Name = name;
        }
    }
}