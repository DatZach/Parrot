using System;

namespace Parrot
{
    internal static class Program
    {
        internal static RuntimeConfig Config { get; private set; }

        public static void Main(string[] args)
        {
            Config = RuntimeConfig.FromCommandLine(args);
            if (Config == null)
            {
                RuntimeConfig.PrintHelp();
                return;
            }

            var manager = new SpeechServiceManager();
            
            Console.Write("Initializing... ");
            manager.Initialize();
            Console.WriteLine("Done!");

            manager.Run();

            manager.Shutdown();
        }
    }
}
