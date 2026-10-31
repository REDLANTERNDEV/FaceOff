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
        /// Used only when FaceOff has never seen the services enabled (e.g. first run after
        /// the user already disabled them with a script). Manual start is what FACEIT needs
        /// to be launchable again (FACEIT support also recommends Manual for FACEITService).
        /// </summary>
        public const StartType FallbackStartType = StartType.Demand;

        /// <summary>
        /// Anti-cheat tray client (without .exe). Always closed before the services are stopped,
        /// otherwise it can start them again. Same as choosing Exit in its tray menu.
        /// </summary>
        public static readonly string[] AntiCheatProcesses = { "faceitclient" };

        /// <summary>The FACEIT app itself. Closed too unless the user keeps it; it doesn't block disabling the anti-cheat.</summary>
        public static readonly string[] AppProcesses = { "FACEIT" };
    }
}
