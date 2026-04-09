using ColorVision.UI;
using ColorVision.UI.Menus;
using ColorVision.UI.Shell;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ColorVision
{
    public class CommadnInitialized : MainWindowInitializedBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CommadnInitialized));

        public override Task Initialize()
        {
            log.Info("CommadnInitialized");
            try
            {
                var parser = ArgumentParser.GetInstance();
                parser.AddArgument("cmd", false, "c");
                parser.Parse();

                string cmd = parser.GetValue("cmd");
                if (cmd != null)
                {
                    List<IMenuItem> IMenuItems = new List<IMenuItem>();
                    foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
                    {
                        foreach (Type type in assembly.GetTypes().Where(t => typeof(IMenuItem).IsAssignableFrom(t) && !t.IsAbstract))
                        {
                            if (Activator.CreateInstance(type) is IMenuItem menuitem)
                            {
                                IMenuItems.Add(menuitem);
                            }
                        }
                    }
                    if (IMenuItems.Find(a => a.GuidId == cmd) is IMenuItem menuitem1)
                    {
                        log.Info($"Execute{menuitem1.Header}");
                        menuitem1.Command?.Execute(this);
                    }
                }
            }
            catch(Exception ex)
            {
                log.Error(ex);
            }

            return Task.CompletedTask;  
        }
    }
}
