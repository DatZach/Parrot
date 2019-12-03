using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace Parrot.Commands
{
    [Action]
    public sealed class SetForegroundProcess : IAction
    {
        public void Execute(CommandEngine engine, ActionConfig action, Dictionary<string, string> args)
        {
            Func<IntPtr, bool> isTargetWindow;
            switch (action["Method"])
            {
                case "ProcessName":
                    isTargetWindow = (hwnd) =>
                    {
                        var processes = Process.GetProcessesByName(action["ProcessName"]);
                        return processes.Any(x => x.MainWindowHandle == hwnd && x.MainWindowHandle != IntPtr.Zero);
                    };
                    break;
                case "Keywords":
                {
                    isTargetWindow = (hwnd) =>
                    {
                        var keywordsStr = action["Keywords"];
                        foreach (var kvp in args)
                            keywordsStr = keywordsStr.Replace("%" + kvp.Key + '%', kvp.Value);
                        
                        uint pid;
                        WinApi.GetWindowThreadProcessId(hwnd, out pid);
                        var process = Process.GetProcessById((int)pid);
                        
                        string title = process.ProcessName + " " + WinApi.GetWindowTitle(hwnd);
                        return keywordsStr.Split(' ')
                            .Any(x => title.IndexOf(x, StringComparison.OrdinalIgnoreCase) != -1);
                    };
                    break;
                }
                case "Last":
                    SendKeys.SendWait("%{TAB}");
                    return;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var windows = GetAllWindows();

            var foreground = WinApi.GetForegroundWindow();
            bool skip = true;
            for (var i = 0; i < windows.Count*2; ++i)
            {
                var window = windows[i % windows.Count];
                if (skip)
                {
                    if (foreground == window || i >= windows.Count)
                        skip = false;
                    
                    continue;
                }
                
                if (!isTargetWindow(window))
                    continue;

                ActivateWindow(window);
                break;
            }
        }

        private List<IntPtr> GetAllWindows()
        {
            var list = new List<IntPtr>();

            WinApi.EnumWindows((hwnd, lParam) =>
            {
                if (!WinApi.IsWindowVisible(hwnd))
                    return true;
                
                list.Add(hwnd);
                return true;
            }, IntPtr.Zero);

            return list.OrderBy(x => x.ToInt32()).ToList();
        }

        public static void ActivateWindow(IntPtr hwnd)
        {
            WinApi.ShowWindow(hwnd, WinApi.ShowWindowEnum.Show);
            WinApi.SetForegroundWindow(hwnd);
            WinApi.SwitchToThisWindow(hwnd, true);
        }
    }
}
