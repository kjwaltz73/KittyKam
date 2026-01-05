using System;
using System.Threading;
using KittyKam.Shared;
using KittyKamHost;

namespace KittyKam.Bootloader
{
    public class BootloaderProgram
    {
        public static void Main()
        {
            System.Diagnostics.Debug.WriteLine("=== BOOTLOADER START ===");
            
            // TODO: set your Wi‑Fi SSID/password here
            WifiHelper.Connect("KYLEW-24", "@[DrunkF!sh]@");
            
            WaitForIp();

            System.Diagnostics.Debug.WriteLine("[Bootloader] Starting AdminServer...");
            
            // Start admin web server on port 80
            var statusProvider = new BootloaderStatusProvider();
            var admin = new AdminServer(statusProvider, "KittyKam Bootloader");
            
            try
            {
                admin.Start(80);
                System.Diagnostics.Debug.WriteLine("[Bootloader] AdminServer started successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Bootloader] AdminServer failed: {ex.Message}");
            }

            // Keep running
            System.Diagnostics.Debug.WriteLine("[Bootloader] Entering main loop");
            Thread.Sleep(Timeout.Infinite);
        }

        private static void WaitForIp(int timeoutMs = 15000)
        {
            System.Diagnostics.Debug.WriteLine("[Net] Waiting for IP address...");
            var start = System.DateTime.UtcNow;
            while ((System.DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
            {
                var ip = NetworkInfo.GetIpAddress();
                if (!string.IsNullOrEmpty(ip) && ip != "0.0.0.0")
                {
                    System.Diagnostics.Debug.WriteLine($"[Net] Device IP: {ip}");
                    return;
                }
                Thread.Sleep(100);
            }
            System.Diagnostics.Debug.WriteLine("[Net] Timed out waiting for IP.");
        }
    }
}
