using ColorVision.Common.MVVM;
using System;
using System.Windows.Controls;

namespace ColorVision.Engine.Abstractions
{
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
