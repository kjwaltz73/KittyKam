using System;
using System.Diagnostics;
using nanoFramework.Networking;

namespace KittyKamHost
{
    public static class Wifi
    {
        public static void Connect(string ssid, string password)
        {
            Debug.WriteLine($"[WiFi] Connecting to SSID '{ssid}'…");

            // Simplest overload: SSID + password + requiresDateTime flag
            // - requiresDateTime = false unless you need NTP time right away.
            bool success = WifiNetworkHelper.ConnectDhcp(
                ssid: ssid,
                password: password,
                requiresDateTime: false
            );

            if (!success)
            {
                Debug.WriteLine($"[WiFi] Connect failed. Status: {WifiNetworkHelper.Status}");
                if (WifiNetworkHelper.HelperException != null)
                    Debug.WriteLine($"[WiFi] Exception: {WifiNetworkHelper.HelperException.Message}");
            }
            else
            {
                Debug.WriteLine("[WiFi] Connected and DHCP acquired.");
            }

            // Optional small settle delay
            DelayMs(1000);
        }

        private static void DelayMs(int ms)
        {
            var start = DateTime.UtcNow;
            while ((DateTime.UtcNow - start).TotalMilliseconds < ms) { /* spin-wait */ }
        }
    }
}
