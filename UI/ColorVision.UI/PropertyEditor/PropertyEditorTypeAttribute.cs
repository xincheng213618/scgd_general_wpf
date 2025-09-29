using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Data;

///这里划到xi
namespace System.ComponentModel
{
    public enum PropertyEditorType
    {
        Default,
        Bool,
        Text,
        Enum,
        TextSelectFolder,
        TextSelectFile
    }
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

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Event)]
    public class PropertyEditorTypeAttribute : Attribute
    {
        public static readonly PropertyEditorTypeAttribute Default = new PropertyEditorTypeAttribute();
        public virtual PropertyEditorType PropertyEditorType => PropertyEditorTypeValue;
        protected PropertyEditorType PropertyEditorTypeValue { get; set; }
        public UpdateSourceTrigger UpdateSourceTrigger { get; set; } = UpdateSourceTrigger.PropertyChanged;
        /// <summary>
        /// 可选: 自定义编辑器类型(实现 IPropertyEditor 即可, 在运行时由调用方用反射实例化并 cast)。Attribute 中不直接引用接口, 以避免编译依赖问题。
        /// </summary>
        public Type? EditorType { get; }

        public PropertyEditorTypeAttribute(PropertyEditorType propertyEditorType) => PropertyEditorTypeValue = propertyEditorType;
        public PropertyEditorTypeAttribute() : this(PropertyEditorType.Default) { }

        public PropertyEditorTypeAttribute(Type editorType)
        {
            EditorType = editorType;
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            if (obj is PropertyEditorTypeAttribute other)
                return other.PropertyEditorTypeValue == PropertyEditorTypeValue && other.EditorType == EditorType;
            return false;
        }
        public override int GetHashCode() => HashCode.Combine(PropertyEditorTypeValue, EditorType);
        public override bool IsDefaultAttribute() => Equals(Default);
    }
}
