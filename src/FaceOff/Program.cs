using System;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace FaceOff
{
    internal static class Program
    {
        const int ExitOk = 0, ExitError = 1, ExitRebootRequired = 2;

        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                var app = new App();
                app.InitializeComponent();
                return app.Run(new MainWindow());
            }
            return RunCli(args);
        }

        static int RunCli(string[] args)
        {
            // WinExe has no console of its own: borrow the caller's, or open one and keep it visible.
            bool ownConsole = !AttachConsole(ATTACH_PARENT_PROCESS);
            if (ownConsole) AllocConsole();
            Console.SetOut(new System.IO.StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Console.WriteLine();

            int code;
            try { code = Execute(args); }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
                code = ExitError;
            }

            if (ownConsole)
            {
                Console.WriteLine("\nPress any key to close.");
                Console.ReadKey(true);
            }
            return code;
        }

        static int Execute(string[] args)
        {
            string command = args[0].ToLowerInvariant();
            bool reboot = Array.Exists(args, a => a.Equals("--reboot", StringComparison.OrdinalIgnoreCase));
            bool keepApp = Array.Exists(args, a => a.Equals("--keep-app", StringComparison.OrdinalIgnoreCase));

            if (command == "--help" || command == "-h" || command == "/?")
            {
                PrintHelp();
                return ExitOk;
            }
            if (!IsAdmin())
            {
                Console.WriteLine("FaceOff must run as administrator.");
                return ExitError;
            }

            var toggler = new Toggler(StateStore.Default());
            Result result;
            switch (command)
            {
                case "--status":
                    PrintStatus(toggler.GetStatus());
                    return ExitOk;
                case "--disable":
                    result = toggler.Disable(keepApp);
                    break;
                case "--enable":
                    result = toggler.Enable();
                    break;
                default:
                    Console.WriteLine("Unknown option: " + args[0]);
                    PrintHelp();
                    return ExitError;
            }

            foreach (var line in result.Log) Console.WriteLine("  " + line);
            Console.WriteLine();
            PrintStatus(toggler.GetStatus());

            if (!result.RebootRequired) return ExitOk;
            if (reboot)
            {
                Console.WriteLine("Restarting in 5 seconds...");
                Toggler.Reboot();
                return ExitOk;
            }
            Console.WriteLine("Restart Windows to finish (or run again with --reboot).");
            return ExitRebootRequired;
        }

        static void PrintStatus(Status status)
        {
            Console.WriteLine("FACEIT anti-cheat: " + Describe(status.State));
            foreach (var s in new[] { status.Driver, status.Service })
                Console.WriteLine(s.Installed
                    ? $"  {s.Name,-14} {(s.Running ? "running" : "stopped"),-8} start={s.StartType}"
                    : $"  {s.Name,-14} not installed");
        }

        public static string Describe(AntiCheatState state)
        {
            switch (state)
            {
                case AntiCheatState.On: return "ON";
                case AntiCheatState.Off: return "OFF";
                case AntiCheatState.OnAfterRestart: return "ON AFTER RESTART";
                case AntiCheatState.OffAfterRestart: return "OFF AFTER RESTART";
                default: return "NOT INSTALLED";
            }
        }

        static void PrintHelp()
        {
            Console.WriteLine("FaceOff - turn FACEIT's kernel anti-cheat off when you're not playing.");
            Console.WriteLine();
            Console.WriteLine("  FaceOff.exe                     open the window");
            Console.WriteLine("  FaceOff.exe --status            show driver and service state");
            Console.WriteLine("  FaceOff.exe --disable [--keep-app] [--reboot]");
            Console.WriteLine("                                  close FACEIT, stop the anti-cheat, keep it from loading at boot");
            Console.WriteLine("                                  --keep-app leaves the FACEIT app running");
            Console.WriteLine("  FaceOff.exe --enable  [--reboot]  restore FACEIT's original configuration");
            Console.WriteLine();
            Console.WriteLine("Exit codes: 0 ok, 1 error, 2 restart required.");
        }

        static bool IsAdmin()
        {
            using (var identity = WindowsIdentity.GetCurrent())
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }

        const int ATTACH_PARENT_PROCESS = -1;
        [DllImport("kernel32.dll")] static extern bool AttachConsole(int processId);
        [DllImport("kernel32.dll")] static extern bool AllocConsole();
    }
}
