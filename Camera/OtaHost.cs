
using System;

namespace KittyKamHost
{
    public static class OtaHost
    {
        public static string CurrentVersion { get; private set; } = "0.0.0";
        public static string LastResult { get; private set; } = "n/a";
        public static DateTime LastCheckUtc { get; private set; } = DateTime.MinValue;

        public static void Init(string version) => CurrentVersion = version;

        // Synchronous check (no async/await)
        public static void CheckForUpdate(bool manual)
        {
            LastCheckUtc = DateTime.UtcNow;

            // TODO: Replace this stub with real manifest fetch + download + verify + load
            // For now, just mark success so you see updates on the admin page.
            DelayMilliseconds(200); // small pause to simulate work
            LastResult = "Stubbed (sync): no update logic yet";
        }

        // Minimal delay helper without Tasks
        private static void DelayMilliseconds(int ms)
        {
            var start = DateTime.UtcNow;
            while ((DateTime.UtcNow - start).TotalMilliseconds < ms)
            {
                // yield CPU briefly
            }
        }
    }
}
