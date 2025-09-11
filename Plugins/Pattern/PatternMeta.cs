using ColorVision.Common.MVVM;

namespace Pattern
{
    public class PatternMeta : ViewModelBase
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public IPattern Pattern { get; set; } 
    }
}
