using System;
using System.Collections.Generic;

namespace Parrot
{
    public interface IAction
    {
        void Execute(CommandEngine engine, ActionConfig action, Dictionary<string, string> args);
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ActionAttribute : Attribute
    {
        
    }
}
