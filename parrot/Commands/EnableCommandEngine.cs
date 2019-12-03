using System;
using System.Collections.Generic;

namespace Parrot.Commands
{
    [Action]
    public sealed class EnableCommandEngine : IAction
    {
        public void Execute(CommandEngine engine, ActionConfig action, Dictionary<string, string> args)
        {
            engine.Enabled = true;
            Console.WriteLine("Activated");
        }
    }
}
