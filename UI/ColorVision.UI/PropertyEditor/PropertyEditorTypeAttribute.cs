using System.Diagnostics.CodeAnalysis;
using System.Windows.Data;

///这里划到xi
namespace System.ComponentModel
{
    public enum CommandType
    {
        Normal,
        Highlighted,
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)] 
    public sealed class CommandDisplayAttribute : Attribute
    {
        public static readonly CommandDisplayAttribute Default;
        public string DisplayName { get; }
        public int Order { get; set; }
        public CommandType CommandType { get; set; } = CommandType.Normal;
        public CommandDisplayAttribute(string displayName) => DisplayName = displayName; 
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Event)]
    public class PropertyVisibilityAttribute : Attribute
    {
        public string PropertyName { get; }
        public bool IsInverted { get; }
        public PropertyVisibilityAttribute(string propertyName ,bool isInverted = false)
        {
            PropertyName = propertyName;
            IsInverted = isInverted;
        }
    }

    /// <summary>
    /// 属性编辑器类型特性: 仅指定一个实现 IPropertyEditor 的类型。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class PropertyEditorTypeAttribute : Attribute
    {
        public static readonly PropertyEditorTypeAttribute Default = new PropertyEditorTypeAttribute();

        public UpdateSourceTrigger UpdateSourceTrigger { get; set; } = UpdateSourceTrigger.PropertyChanged;

        /// <summary>
        /// 自定义编辑器类型 (必须实现 IPropertyEditor). 若为空则使用 TextboxPropertiesEditor。
        /// </summary>
        public Type? EditorType { get; } = typeof(TextboxPropertiesEditor);

        public PropertyEditorTypeAttribute() { }
        public PropertyEditorTypeAttribute(Type editorType)
        {
            EditorType = editorType;
        }
        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            if (obj is PropertyEditorTypeAttribute other)
                return other.EditorType == EditorType;
            return false;
        }
        public override int GetHashCode() => HashCode.Combine(EditorType);
        public override bool IsDefaultAttribute() => Equals(Default);
    }
}
