using System.Collections.Generic;

namespace Parrot.Commands
{
    [Action]
    public sealed class Shutdown : IAction
    {
        public void Execute(CommandEngine engine, ActionConfig action, Dictionary<string, string> args)
        {
            //engine.Shutdown();
        }
    }
}
