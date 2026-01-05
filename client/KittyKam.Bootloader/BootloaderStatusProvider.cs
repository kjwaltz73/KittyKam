using KittyKam.Shared;

namespace KittyKam.Bootloader
{
    public class BootloaderStatusProvider : IStatusProvider
    {
        public string GetVersion() => OtaUpdater.CurrentVersion ?? "unknown";

        public string GetStatusJson()
        {
            var lastCheck = OtaUpdater.LastCheckUtc.ToString("O");
            var lastResult = OtaUpdater.LastResult ?? "none";
            return $"\"lastCheck\":\"{lastCheck}\",\"lastResult\":\"{lastResult}\"";
        }
    }
}
