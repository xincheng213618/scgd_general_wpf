using System;

namespace ColorVision.UI
{
    public interface IPage
    {
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class PageAttribute : Attribute
    {
        public string Title { get; }
        public PageAttribute(string title) => Title = title;
    }
}
