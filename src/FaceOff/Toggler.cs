using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace FaceOff
{
    internal enum AntiCheatState
    {
        NotInstalled,
        On,
        /// <summary>Set to load at boot, but not loaded yet: FACEIT works after the next restart.</summary>
        OnAfterRestart,
        /// <summary>Kept from loading at boot, but still loaded until the next restart.</summary>
        OffAfterRestart,
        Off,
    }

    internal sealed class Status
    {
        public ServiceInfo Driver;
        public ServiceInfo Service;
        /// <summary>The anti-cheat tray client (a process, not a service). Shown, never part of State.</summary>
        public ServiceInfo Client;

        public AntiCheatState State
        {
            get
            {
                if (!Driver.Installed && !Service.Installed) return AntiCheatState.NotInstalled;
                // The driver decides: FACEIT runs only when it loads at boot.
                if (LoadsAtBoot(Driver.StartType))
                    return Driver.Running ? AntiCheatState.On : AntiCheatState.OnAfterRestart;
                return Driver.Running || Service.Running ? AntiCheatState.OffAfterRestart : AntiCheatState.Off;
            }
        }

        public static bool LoadsAtBoot(StartType t) => t == StartType.Boot || t == StartType.System;
    }

    internal sealed class Result
    {
        public bool RebootRequired;
        public readonly List<string> Log = new List<string>();
    }

    /// <summary>The only place that changes anything. Shared by the window and the CLI.</summary>
    internal sealed class Toggler
    {
        // Only the upper bound: waiting ends as soon as the service has stopped.
        static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
        readonly StateStore _store;

        public Toggler(StateStore store) { _store = store; }

        public Status GetStatus() => new Status
        {
            Driver = ServiceControl.Query(Targets.Driver),
            Service = ServiceControl.Query(Targets.Service),
            Client = AntiCheatClient.Query(),
        };

        public Result Disable(bool keepApp)
        {
            var result = new Result();
            var status = GetStatus();
            if (status.State == AntiCheatState.NotInstalled)
                throw new InvalidOperationException("FACEIT anti-cheat is not installed.");

            // 1. Remember how FACEIT was configured before we touch anything.
            // A driver that doesn't load at boot isn't FACEIT's own setting, so it's not worth keeping.
            if (status.Driver.Installed && Status.LoadsAtBoot(status.Driver.StartType))
                _store.Remember(status.Driver.Name, status.Driver.StartType);
            if (status.Service.Installed)
                _store.Remember(status.Service.Name, status.Service.StartType);
            _store.TurnedOffUtc = DateTime.UtcNow;
            _store.Save();

            // 2. Close the anti-cheat client first, otherwise it can restart the service.
            CloseProcesses(Targets.AntiCheatProcesses, result);
            if (!keepApp) CloseProcesses(Targets.AppProcesses, result);

            // 3. Service before driver: the service depends on the driver being loaded.
            foreach (var s in new[] { status.Service, status.Driver })
            {
                if (!s.Installed) continue;
                ServiceControl.SetStartType(s.Name, StartType.Disabled);
                result.Log.Add($"{s.Name}: start type -> Disabled");

                result.Log.Add(ServiceControl.TryStop(s.Name, Timeout)
                    ? $"{s.Name}: stopped"
                    : $"{s.Name}: didn't stop yet");
            }

            // 4. Judge by what is actually loaded now, not by how each stop call went.
            var after = GetStatus();
            result.RebootRequired = after.Driver.Running || after.Service.Running;
            result.Log.Add(result.RebootRequired
                ? "Driver still loaded: restart Windows to unload it"
                : "Driver unloaded. Restart anyway for a clean start");
            return result;
        }

        public Result Enable()
        {
            var result = new Result();
            _store.TurnedOffUtc = null;
            _store.Save();
            var status = GetStatus();
            if (status.State == AntiCheatState.NotInstalled)
                throw new InvalidOperationException("FACEIT anti-cheat is not installed.");

            // Only start types change here. FACEIT won't accept a driver started after boot,
            // so the driver loads on the next restart and the client starts the service itself.
            if (status.Driver.Installed)
            {
                var driver = _store.TryGet(status.Driver.Name, out var saved) && Status.LoadsAtBoot(saved)
                    ? saved : Targets.DriverDefault;
                ServiceControl.SetStartType(status.Driver.Name, driver);
                result.Log.Add($"{status.Driver.Name}: start type -> {driver}");
            }
            if (status.Service.Installed)
            {
                var service = _store.TryGet(status.Service.Name, out var saved) ? saved : Targets.ServiceDefault;
                ServiceControl.SetStartType(status.Service.Name, service);
                result.Log.Add($"{status.Service.Name}: start type -> {service}");
            }

            if (status.Driver.Installed && !status.Driver.Running)
            {
                result.Log.Add($"{status.Driver.Name}: loads on the next restart");
                result.RebootRequired = true;
            }
            return result;
        }

        /// <summary>Asks each process to close like a normal Exit, and forces it only if it doesn't within 3 seconds.</summary>
        static void CloseProcesses(string[] names, Result result)
        {
            foreach (var name in names)
                foreach (var p in Process.GetProcessesByName(name))
                    using (p)
                    {
                        try
                        {
                            int pid = p.Id;
                            if (p.CloseMainWindow() && p.WaitForExit(3000))
                                result.Log.Add($"Closed {name}.exe (PID {pid})");
                            else
                            {
                                p.Kill();
                                p.WaitForExit(5000);
                                result.Log.Add($"Force-closed {name}.exe (PID {pid})");
                            }
                        }
                        catch (Exception e) { result.Log.Add($"Could not close {name}.exe: {e.Message}"); }
                    }
        }

        /// <summary>True if Turn off ran and Windows hasn't restarted since.</summary>
        public bool CleanRestartPending =>
            _store.TurnedOffUtc.HasValue && LastBootUtc < _store.TurnedOffUtc.Value;

        static DateTime LastBootUtc => DateTime.UtcNow - TimeSpan.FromMilliseconds(GetTickCount64());

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern ulong GetTickCount64();

        public static void Reboot()
        {
            Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 5 /c \"FaceOff: restarting to apply FACEIT anti-cheat changes.\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        }
    }
}
