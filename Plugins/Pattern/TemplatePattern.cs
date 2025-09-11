using ColorVision.Common.MVVM;

namespace Pattern
{
    public class TemplatePattern:ViewModelBase
    {
        public PatternWindowConfig PatternWindowConfig { get; set; }

        public string PatternName { get; set; }
        public string Config { get; set; }
    }
}
