using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Parrot.Commands
{
    [Action]
    public sealed class Reload : IAction
    {
        public void Execute(CommandEngine engine, ActionConfig action, Dictionary<string, string> args)
        {
            if (engine.Enabled)
            {
                Console.WriteLine("Disable engine before reloading...");
                return;
            }

            Console.WriteLine("Reloading...");
            Process.Start(Assembly.GetExecutingAssembly().GetName().CodeBase); 
            Environment.Exit(1);
        }
    }
}
