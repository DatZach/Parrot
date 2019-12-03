using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parrot.Commands
{
    [Action]
    public sealed class ExecuteShell : IAction
    {
        public void Execute(CommandEngine engine, ActionConfig action, Dictionary<string, string> args)
        {
            try
            {
                var path = action["Path"];
                Console.WriteLine("Opening {0}", path);
                Process.Start(new ProcessStartInfo(path));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
            }
        }
    }
}
