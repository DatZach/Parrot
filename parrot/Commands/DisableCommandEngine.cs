using System;
using System.Collections.Generic;

namespace Parrot.Commands
{
    [Action]
    public sealed class DisableCommandEngine : IAction
    {
        public void Execute(CommandEngine engine, ActionConfig action, Dictionary<string, string> args)
        {
            engine.Enabled = false;
            Console.WriteLine("Deactivated");
        }
    }
}
