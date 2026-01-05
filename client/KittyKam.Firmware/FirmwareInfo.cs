using System;

namespace KittyKam.Firmware
{
    public static class FirmwareInfo
    {
        private static string _currentVersion = "0.0.0";

        public static void Init(string version)
        {
            _currentVersion = version;
        }

        public static string GetVersion()
        {
            return _currentVersion;
        }
    }
}