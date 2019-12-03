using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace Parrot.Commands
{
    [Action]
    public sealed class Keyboard : IAction
    {
        public void Execute(CommandEngine engine, ActionConfig action, Dictionary<string, string> args)
        {
            try
            {
                string repeatStr;
                int repeat;
                if (!args.TryGetValue("repeat", out repeatStr) || !int.TryParse(repeatStr, out repeat))
                    repeat = 1;

                repeat = Math.Max(repeat, 1);
                
                var value = action["Key"];
                foreach (var kvp in args)
                    value = value.Replace("%" + kvp.Key + '%', kvp.Value);

                for (int i = 0; i < repeat; ++i)
                {
                    bool winKey = value.IndexOf("{WIN}") != -1;
                    if (winKey)
                    {
                        value = value.Replace("{WIN}", "");
                        KeyDown(Keys.LWin);
                    }

                    SendKeys.SendWait(value);

                    if (winKey)
                        KeyUp(Keys.LWin);

                    Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

        private const int KEYEVENTF_EXTENDEDKEY = 1;
        private const int KEYEVENTF_KEYUP = 2;

        public static void KeyDown(Keys vKey)
        {
            keybd_event((byte)vKey, 0, KEYEVENTF_EXTENDEDKEY, 0);
        }

        public static void KeyUp(Keys vKey)
        {
            keybd_event((byte)vKey, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);
        }
    }
}
