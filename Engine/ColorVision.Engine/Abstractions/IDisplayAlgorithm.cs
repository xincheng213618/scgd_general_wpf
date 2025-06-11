using ColorVision.Common.MVVM;
using System;
using System.Windows.Controls;

namespace ColorVision.Engine.Abstractions
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class DisplayAlgorithmAttribute : Attribute
    {
        public int Order { get; }
        public string Name { get; }
        public string Group { get; }

        public DisplayAlgorithmAttribute(int order, string name, string group)
        {
            Order = order;
            Name = name;
            Group = group;
        }
    }

    public interface IDisplayAlgorithm
    {
        public int Order { get; set; }
        public string Name { get; set; }

        public string Group { get; set; }
        public UserControl GetUserControl();
    }

    public abstract class DisplayAlgorithmBase : ViewModelBase, IDisplayAlgorithm
    {
        public virtual string Name { get; set; }
        public virtual int Order { get; set; }

        public virtual string Group { get; set; }

        public virtual UserControl GetUserControl()
        {
            throw new NotImplementedException();
        }
    };
}
