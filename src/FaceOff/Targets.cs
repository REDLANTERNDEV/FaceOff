namespace FaceOff
{
    /// <summary>What FaceOff touches. Nothing outside this list is ever modified.</summary>
    internal static class Targets
    {
        /// <summary>FACEIT anti-cheat kernel driver (C:\Program Files\FACEIT AC\FACEIT_AC.sys).</summary>
        public const string Driver = "faceit";

        /// <summary>FACEIT anti-cheat user-mode service.</summary>
        public const string Service = "FACEITService";

        /// <summary>
        /// FACEIT only accepts a driver that was loaded while Windows booted, so "on" means
        /// System start. Used when no valid original was saved (e.g. disabled earlier by a script).
        /// </summary>
        public const StartType DriverDefault = StartType.System;

        /// <summary>FACEIT support recommends Manual for FACEITService; the client starts it.</summary>
        public const StartType ServiceDefault = StartType.Demand;

        /// <summary>
        /// Anti-cheat tray client (without .exe). Always closed before the services are stopped,
        /// otherwise it can start them again. Same as choosing Exit in its tray menu.
        /// </summary>
        public static readonly string[] AntiCheatProcesses = { "faceitclient" };

        /// <summary>The FACEIT app itself. Closed too unless the user keeps it; it doesn't block disabling the anti-cheat.</summary>
        public static readonly string[] AppProcesses = { "FACEIT" };
    }
}
