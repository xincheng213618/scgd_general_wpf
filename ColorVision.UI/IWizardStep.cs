using ColorVision.Common.MVVM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.UI
{
    public interface IWizardStep
    {
        public int Order { get; }
        public string Title { get; }
        public string Description { get; }
        public RelayCommand? RelayCommand { get; }
    }
}
