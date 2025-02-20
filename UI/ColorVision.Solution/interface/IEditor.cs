using ColorVision.Common.Utilities;
using ColorVision.UI;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms.VisualStyles;
using System.Windows.Media;

namespace ColorVision.Solution
{

    public class IEditorManager
    {
        public static IEditorManager Instance { get; set; } = new IEditorManager();
        public Dictionary<string, Type> EditorMappings { get; set; }
        public IEditorManager()
        {
            EditorMappings = new Dictionary<string, Type>();
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(IEditor).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                    {
                        if (Activator.CreateInstance(type) is IEditor editor)
                        {
                            EditorMappings.Add(editor.Extension, type);
                        }
                    }
                }
            }
        }
        public IEditor? GetEditor(string FullPath)
        {
            FileInfo fileInfo = new FileInfo(FullPath);
            string extension = fileInfo.Extension;
            List<Type> matchingTypes = new List<Type>();
            if (EditorMappings.TryGetValue(extension, out Type specificTypes))
            {
                matchingTypes.Add(specificTypes);
            }
            foreach (var key in EditorMappings.Keys)
            {
                if (key.Contains(extension))
                    matchingTypes.Add(EditorMappings[key]);
            }
            foreach (var key in EditorMappings.Keys)
            {
                var subKeys = key.Split('|');
                foreach (var subKey in subKeys)
                {
                    if (new Regex("^" + Regex.Escape(subKey).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase).IsMatch(extension))
                    {
                        matchingTypes.Add(EditorMappings[key]);
                        break;
                    }
                }
            }
            if (matchingTypes.Count > 0)
            {
                if (Activator.CreateInstance(matchingTypes[0]) is IEditor iEditor)
                {
                    return iEditor;
                }
            }
            return null;
        }
    }

    public interface IEditor
    {
        string Extension { get; }
        string Name { get; }
        Control? Open(string FilePath);
    }

    public abstract class IEditorBase: IEditor
    {
        public virtual string Extension { get; set; }
        public virtual string Name { get; set; }

        public ImageSource Icon { get; set; }
        public abstract Control? Open(string FilePath);
    }

    public class SystemEditor : IEditorBase
    {
        public override string Extension => ".*";
        public override Control? Open(string FilePath)
        {
            PlatformHelper.Open(FilePath);
            return null;
        }
    }
}