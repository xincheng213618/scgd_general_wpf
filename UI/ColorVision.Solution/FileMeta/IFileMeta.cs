using ColorVision.Common.MVVM;
using System.IO;
using System.Windows.Media;


namespace ColorVision.Solution.FileMeta
{
    /// <summary>
    /// Attribute to register file meta for specific extensions
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class FileMetaForExtensionAttribute : Attribute
    {
        public string? Name { get; }
        public string[] Extensions { get; }
        public bool IsDefault { get; }

        public FileMetaForExtensionAttribute(string extensions, string? name = null, bool isDefault = false)
        {
            Name = name;
            Extensions = extensions.Split('|');
            IsDefault = isDefault;
        }
    }

    /// <summary>
    /// Attribute to register generic file meta that applies to all file types
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class GenericFileMetaAttribute : Attribute
    {
        public string? Name { get; }
        
        public GenericFileMetaAttribute(string? name = null)
        {
            Name = name;
        }
    }

    public interface IFileMeta
    {
        int Order { get; }
        string Name { get; set; }

        public FileInfo FileInfo {get;set;}

        ImageSource? Icon { get; set; }
    }

    /// <summary>
    /// Marks a file type that can be executed by the solution terminal command.
    /// </summary>
    public interface IScriptFileMeta { }


    public abstract class FileMetaBase : ViewModelBase, IFileMeta
    {
        public virtual int Order { get; } = 1;
        public virtual string Name { get; set; }
        public FileInfo FileInfo { get; set; }
        public ImageSource? Icon { get; set; }
    }



}
