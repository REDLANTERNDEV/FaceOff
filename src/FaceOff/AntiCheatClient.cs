using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace FaceOff
{
    /// <summary>Finds and opens FACEIT's anti-cheat tray client (faceitclient.exe).</summary>
    internal static class AntiCheatClient
    {
        /// <summary>
        /// The client sits next to faceitservice.exe, so its folder comes from the service's
        /// registered path instead of a hardcoded install location. Null if not found.
        /// </summary>
        public static string FindPath()
        {
            using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + Targets.Service))
            {
                var image = key?.GetValue("ImagePath") as string;
                if (string.IsNullOrWhiteSpace(image)) return null;

                image = Environment.ExpandEnvironmentVariables(image.Trim());
                // ImagePath is usually quoted and may carry arguments: "C:\...\faceitservice.exe" -x
                if (image.StartsWith("\""))
                    image = image.Substring(1, image.IndexOf('"', 1) - 1);

                var dir = Path.GetDirectoryName(image);
                if (dir == null) return null;
                var path = Path.Combine(dir, Targets.AntiCheatProcesses[0] + ".exe");
                return File.Exists(path) ? path : null;
            }
        }

        public static ServiceInfo Query()
        {
            var running = Process.GetProcessesByName(Targets.AntiCheatProcesses[0]);
            foreach (var p in running) p.Dispose();
            return new ServiceInfo
            {
                Name = Targets.AntiCheatProcesses[0],
                Installed = FindPath() != null,
                Running = running.Length > 0,
            };
        }

        /// <summary>
        /// Opens the client through Explorer so it runs with the user's normal rights rather than
        /// inheriting FaceOff's admin token. If the client needs admin, it asks for it itself.
        /// </summary>
        public static void Open()
        {
            var path = FindPath() ?? throw new FileNotFoundException("FACEIT anti-cheat client not found.");
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = false });
        }
    }
}
