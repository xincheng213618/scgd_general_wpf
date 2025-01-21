using System.Diagnostics.CodeAnalysis;

namespace ColorVision.UI.PropertyEditor
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
        CronExpression
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Event)]
    public class PropertyEditorTypeAttribute : Attribute
    {
        public static readonly PropertyEditorTypeAttribute Default = new PropertyEditorTypeAttribute();

        public virtual PropertyEditorType PropertyEditorType => PropertyEditorTypeValue;

        protected PropertyEditorType PropertyEditorTypeValue { get; set; }

        public PropertyEditorTypeAttribute()
            : this(PropertyEditorType.Default)
        {
        }

        public PropertyEditorTypeAttribute(PropertyEditorType  propertyEditorType)
        {
            PropertyEditorTypeValue = propertyEditorType;
        }

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
