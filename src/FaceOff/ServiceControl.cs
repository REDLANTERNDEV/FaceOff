using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace FaceOff
{
    /// <summary>Mirrors the SERVICE_*_START values used by the Service Control Manager.</summary>
    internal enum StartType : uint
    {
        Boot = 0,
        System = 1,
        Auto = 2,
        Demand = 3,
        Disabled = 4,
    }

    internal sealed class ServiceInfo
    {
        public string Name;
        public bool Installed;
        public StartType StartType;
        public bool Running;
    }

    /// <summary>Thin wrapper over the Service Control Manager. Works for services and kernel drivers.</summary>
    internal static class ServiceControl
    {
        public static ServiceInfo Query(string name)
        {
            var info = new ServiceInfo { Name = name };
            try
            {
                using (var sc = new ServiceController(name))
                {
                    var status = sc.Status; // throws if the service doesn't exist
                    info.Installed = true;
                    info.Running = status != ServiceControllerStatus.Stopped;
                    info.StartType = (StartType)(uint)sc.StartType;
                }
            }
            catch (InvalidOperationException)
            {
                info.Installed = false;
            }
            return info;
        }

        public static void SetStartType(string name, StartType startType)
        {
            IntPtr scm = OpenSCManager(null, null, SC_MANAGER_CONNECT);
            if (scm == IntPtr.Zero) throw new Win32Exception();
            try
            {
                IntPtr svc = OpenService(scm, name, SERVICE_CHANGE_CONFIG);
                if (svc == IntPtr.Zero) throw new Win32Exception();
                try
                {
                    if (!ChangeServiceConfig(svc, SERVICE_NO_CHANGE, (uint)startType, SERVICE_NO_CHANGE,
                            null, null, IntPtr.Zero, null, null, null, null))
                        throw new Win32Exception();
                }
                finally { CloseServiceHandle(svc); }
            }
            finally { CloseServiceHandle(scm); }
        }

        /// <summary>Returns true if the service is stopped afterwards.</summary>
        public static bool TryStop(string name, TimeSpan timeout)
        {
            try
            {
                using (var sc = new ServiceController(name))
                {
                    if (sc.Status == ServiceControllerStatus.Stopped) return true;
                    if (sc.Status != ServiceControllerStatus.StopPending) sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
                    return true;
                }
            }
            catch (Exception e) when (e is InvalidOperationException || e is System.ServiceProcess.TimeoutException)
            {
                // Kernel anti-cheat drivers usually refuse to unload while Windows is running.
                return false;
            }
        }

        /// <summary>Returns true if the service is running afterwards.</summary>
        public static bool TryStart(string name, TimeSpan timeout)
        {
            try
            {
                using (var sc = new ServiceController(name))
                {
                    if (sc.Status == ServiceControllerStatus.Running) return true;
                    if (sc.Status != ServiceControllerStatus.StartPending) sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, timeout);
                    return true;
                }
            }
            catch (Exception e) when (e is InvalidOperationException || e is System.ServiceProcess.TimeoutException)
            {
                return false;
            }
        }

        const uint SC_MANAGER_CONNECT = 0x0001;
        const uint SERVICE_CHANGE_CONFIG = 0x0002;
        const uint SERVICE_NO_CHANGE = 0xFFFFFFFF;

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr OpenSCManager(string machineName, string databaseName, uint desiredAccess);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr OpenService(IntPtr scManager, string serviceName, uint desiredAccess);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool ChangeServiceConfig(IntPtr service, uint serviceType, uint startType, uint errorControl,
            string binaryPathName, string loadOrderGroup, IntPtr tagId, string dependencies,
            string serviceStartName, string password, string displayName);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool CloseServiceHandle(IntPtr handle);
    }
}
