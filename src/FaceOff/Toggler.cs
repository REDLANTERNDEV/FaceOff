using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace FaceOff
{
    internal enum AntiCheatState
    {
        NotInstalled,
        On,
        /// <summary>Start types are disabled but the driver is still loaded until the next restart.</summary>
        OffAfterRestart,
        Off,
    }

    internal sealed class Status
    {
        public ServiceInfo Driver;
        public ServiceInfo Service;

        public AntiCheatState State
        {
            get
            {
                if (!Driver.Installed && !Service.Installed) return AntiCheatState.NotInstalled;
                bool disabled = IsDisabled(Driver) && IsDisabled(Service);
                if (!disabled) return AntiCheatState.On;
                return Driver.Running || Service.Running ? AntiCheatState.OffAfterRestart : AntiCheatState.Off;
            }
        }

        static bool IsDisabled(ServiceInfo s) => !s.Installed || s.StartType == StartType.Disabled;
    }

    internal sealed class Result
    {
        public bool RebootRequired;
        public readonly List<string> Log = new List<string>();
    }

    /// <summary>The only place that changes anything. Shared by the window and the CLI.</summary>
    internal sealed class Toggler
    {
        static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
        readonly StateStore _store;

        public Toggler(StateStore store) { _store = store; }

        public Status GetStatus() => new Status
        {
            Driver = ServiceControl.Query(Targets.Driver),
            Service = ServiceControl.Query(Targets.Service),
        };

        public Result Disable()
        {
            var result = new Result();
            var status = GetStatus();
            if (status.State == AntiCheatState.NotInstalled)
                throw new InvalidOperationException("FACEIT anti-cheat is not installed.");

            // 1. Remember how FACEIT was configured before we touch anything.
            foreach (var s in new[] { status.Driver, status.Service })
                if (s.Installed) _store.Remember(s.Name, s.StartType);
            _store.Save();

            // 2. Close the client first, otherwise it can restart the service.
            foreach (var name in Targets.Processes)
                foreach (var p in Process.GetProcessesByName(name))
                    using (p)
                    {
                        try { p.Kill(); p.WaitForExit(5000); result.Log.Add($"Closed {name}.exe (PID {p.Id})"); }
                        catch (Exception e) { result.Log.Add($"Could not close {name}.exe: {e.Message}"); }
                    }

            // 3. Service before driver: the service depends on the driver being loaded.
            foreach (var s in new[] { status.Service, status.Driver })
            {
                if (!s.Installed) continue;
                ServiceControl.SetStartType(s.Name, StartType.Disabled);
                result.Log.Add($"{s.Name}: start type -> Disabled");

                if (ServiceControl.TryStop(s.Name, Timeout))
                    result.Log.Add($"{s.Name}: stopped");
                else
                {
                    result.Log.Add($"{s.Name}: still running, a restart will unload it");
                    result.RebootRequired = true;
                }
            }
            return result;
        }

        public Result Enable()
        {
            var result = new Result();
            var status = GetStatus();
            if (status.State == AntiCheatState.NotInstalled)
                throw new InvalidOperationException("FACEIT anti-cheat is not installed.");

            // Driver before service, the reverse of Disable.
            foreach (var s in new[] { status.Driver, status.Service })
            {
                if (!s.Installed) continue;
                if (!_store.TryGet(s.Name, out var original))
                {
                    original = s.StartType == StartType.Disabled ? Targets.FallbackStartType : s.StartType;
                    result.Log.Add($"{s.Name}: no saved original, using {original}");
                }
                ServiceControl.SetStartType(s.Name, original);
                result.Log.Add($"{s.Name}: start type -> {original}");

                if (ServiceControl.TryStart(s.Name, Timeout))
                    result.Log.Add($"{s.Name}: running");
                else if (original == StartType.Boot || original == StartType.System)
                {
                    result.Log.Add($"{s.Name}: loads at boot, restart to load it");
                    result.RebootRequired = true;
                }
                else
                    result.Log.Add($"{s.Name}: not started now, the FACEIT client will start it");
            }
            return result;
        }

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
