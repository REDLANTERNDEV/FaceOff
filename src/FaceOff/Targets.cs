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

        /// <summary>Process names (without .exe) closed before the services are stopped: the AC client and the FACEIT app.</summary>
        public static readonly string[] Processes = { "faceitclient", "FACEIT" };
    }
}
