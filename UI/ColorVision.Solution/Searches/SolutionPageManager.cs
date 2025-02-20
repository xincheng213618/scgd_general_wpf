using ColorVision.UI;
using System.Windows.Controls;

namespace ColorVision.Solution.Searches
{
    public class SolutionPageManager
    {
        public static SolutionPageManager Instance { get; set; } = new SolutionPageManager();
        public SolutionPageManager()
        {
            Pages = new Dictionary<string, Type>();
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(ISolutionPage).IsAssignableFrom(type) && !type.IsInterface &&!type.IsAbstract)
                    {
                        if (Activator.CreateInstance(type) is ISolutionPage page)
                        {
                            Pages.Add(page.PageTitle, type);
                        }
                    }
                }
            }
        }
        public Page GetPage(string? pageTitle, Frame frame)
        {
            if (Pages.TryGetValue(pageTitle, out Type type))
            {
                if (Activator.CreateInstance(type, frame) is Page page)
                {
                    return page;
                }
            }
            return new Page();
        }

        public Dictionary<string, Type> Pages { get; set; }
    }
}
