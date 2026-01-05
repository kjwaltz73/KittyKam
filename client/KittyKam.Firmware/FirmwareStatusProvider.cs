using System;
using KittyKam.Shared;

namespace KittyKam.Firmware
{
    public class FirmwareStatusProvider : IStatusProvider
    {
        private static readonly DateTime _startTime = DateTime.UtcNow;

        public string GetVersion() => FirmwareInfo.GetVersion();

        public string GetStatusJson()
        {
            // Returns JSON properties (without outer braces) to be merged into main status object
            return $"\"mode\":\"camera\",\"uptime\":\"{GetUptime()}\"";
        }

        private static string GetUptime()
        {
            var uptime = DateTime.UtcNow - _startTime;
            return ((long)uptime.TotalSeconds).ToString();
        }
    }
}
