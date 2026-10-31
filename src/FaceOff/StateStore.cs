using System;
using System.Collections.Generic;
using System.IO;

namespace FaceOff
{
    /// <summary>
    /// Remembers each target's original start type so "Turn on" restores exactly what FACEIT
    /// installed. Plain "name=value" lines in FaceOff.state next to the exe (portable, readable).
    /// </summary>
    internal sealed class StateStore
    {
        readonly string _path;
        readonly Dictionary<string, StartType> _original = new Dictionary<string, StartType>(StringComparer.OrdinalIgnoreCase);

        public StateStore(string path)
        {
            _path = path;
            if (!File.Exists(path)) return;
            foreach (var line in File.ReadAllLines(path))
            {
                var parts = line.Split(new[] { '=' }, 2);
                if (parts.Length != 2) continue;
                string key = parts[0].Trim(), raw = parts[1].Trim();
                if (key.Equals(KeepAppKey, StringComparison.OrdinalIgnoreCase))
                    KeepApp = raw.Equals("true", StringComparison.OrdinalIgnoreCase);
                else if (key.Equals(TurnedOffKey, StringComparison.OrdinalIgnoreCase))
                {
                    if (long.TryParse(raw, out var ticks)) TurnedOffUtc = new DateTime(ticks, DateTimeKind.Utc);
                }
                else if (Enum.TryParse(raw, true, out StartType value) && value != StartType.Disabled)
                    _original[key] = value;
            }
        }

        public static StateStore Default()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            return new StateStore(Path.Combine(dir, "FaceOff.state"));
        }

        /// <summary>Records the start type unless it is Disabled (that would be our own change, not the original).</summary>
        public void Remember(string name, StartType current)
        {
            if (current == StartType.Disabled) return;
            _original[name] = current;
        }

        public bool TryGet(string name, out StartType value) => _original.TryGetValue(name, out value);

        const string KeepAppKey = "KeepApp";

        /// <summary>Window setting: leave the FACEIT app running when turning off. Off by default.</summary>
        public bool KeepApp { get; set; }

        const string TurnedOffKey = "TurnedOffUtc";

        /// <summary>When Turn off last ran. Until Windows restarts after this, a clean restart is suggested.</summary>
        public DateTime? TurnedOffUtc { get; set; }

        public void Save()
        {
            var lines = new List<string> { "# FaceOff: original start types, restored by \"Turn on\"." };
            foreach (var kv in _original) lines.Add(kv.Key + "=" + kv.Value);
            lines.Add(KeepAppKey + "=" + (KeepApp ? "true" : "false"));
            if (TurnedOffUtc.HasValue) lines.Add(TurnedOffKey + "=" + TurnedOffUtc.Value.Ticks);
            File.WriteAllLines(_path, lines);
        }
    }
}
