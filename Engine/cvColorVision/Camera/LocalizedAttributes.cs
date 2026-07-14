using cvColorVision.Properties;
using System;
using System.ComponentModel;

namespace cvColorVision
{
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class LocalizedDisplayNameAttribute : DisplayNameAttribute
    {
        private readonly string _resourceKey;

        public LocalizedDisplayNameAttribute(string resourceKey) : base(resourceKey)
        {
            _resourceKey = resourceKey;
        }

        public override string DisplayName => Resources.GetString(_resourceKey);
    }

    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class LocalizedCategoryAttribute : CategoryAttribute
    {
        private readonly string _resourceKey;

        public LocalizedCategoryAttribute(string resourceKey) : base(resourceKey)
        {
            _resourceKey = resourceKey;
        }

        protected override string GetLocalizedString(string value)
        {
            return Resources.GetString(_resourceKey);
        }
    }
}
