using System;
using System.Collections.Generic;

namespace Parrot.Commands
{
    [Action]
    public sealed class SetContext : IAction
    {
        public void Execute(CommandEngine engine, ActionConfig action, Dictionary<string, string> args)
        {
            engine.Context = action["Value"];
            Console.WriteLine("Context = {0}", engine.Context);
        }
    }
}
