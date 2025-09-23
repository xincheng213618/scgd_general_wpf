using System.Diagnostics.CodeAnalysis;
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
        TextSelectFile,
        TextSerialPort,
        TextBaudRate,
        TextJson,
        CronExpression
    }
    public enum CommandType
    {
        /// <summary>
        /// 普通
        /// </summary>
        Normal,
        /// <summary>
        /// 红色
        /// </summary>
        Highlighted,
    }


    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)] 
    public sealed class CommandDisplayAttribute : Attribute
    {
        public static readonly CommandDisplayAttribute Default;
        public string DisplayName { get; }
        public int Order { get; set; }

        public CommandType CommandType { get; set; } = CommandType.Normal;

        public CommandDisplayAttribute(string displayName) 
        {
            DisplayName = displayName; 
        }


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

        public PropertyEditorTypeAttribute(PropertyEditorType propertyEditorType)
        {
            PropertyEditorTypeValue = propertyEditorType;
        }
        public PropertyEditorTypeAttribute() : this(PropertyEditorType.Default)
        {
        }

        public PropertyEditorTypeAttribute(PropertyEditorType propertyEditorType, object[] itemSourse)
        {
            PropertyEditorTypeValue = propertyEditorType;
            ItemSourse = itemSourse;
        }
        public object[] ItemSourse { get; set; }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            if (obj is PropertyEditorTypeAttribute displayNameAttribute)
            {
                return displayNameAttribute.PropertyEditorTypeValue == PropertyEditorTypeValue;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return PropertyEditorTypeValue.GetHashCode();
        }

        public override bool IsDefaultAttribute()
        {
            return Equals(Default);
        }
    }
}
