using System;
using System.Diagnostics;
using System.Threading;
using KittyKam.Shared;
using KittyKamHost;

namespace KittyKam.Firmware
{
    public class CameraProgram
    {
        private const string FIRMWARE_VERSION = "2.0.0";

        public static void Main()
        {
            Debug.WriteLine("=== FIRMWARE START ===");
            
            // Initialize firmware version
            FirmwareInfo.Init(FIRMWARE_VERSION);
            
            // TODO: set your Wi‑Fi SSID/password here
            Debug.WriteLine("[Firmware] Connecting to WiFi...");
            WifiHelper.Connect("KYLEW-24", "@[DrunkF!sh]@");
            
            WaitForIp();

            Debug.WriteLine("[Firmware] Starting AdminServer...");
            
            // Start admin web server on port 80
            var statusProvider = new FirmwareStatusProvider();
            var admin = new AdminServer(statusProvider, "KittyKam Camera");
            
            try
            {
                admin.Start(80);
                Debug.WriteLine("[Firmware] AdminServer started successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Firmware] AdminServer failed: {ex.Message}");
            }

            // Keep running with heartbeat
            Debug.WriteLine("[Firmware] Entering main loop");
            int counter = 0;
            while (true)
            {
                Debug.WriteLine($"[Firmware] Heartbeat {counter++}");
                Thread.Sleep(5000);
            }
        }

        private static void WaitForIp(int timeoutMs = 15000)
        {
            Debug.WriteLine("[Net] Waiting for IP address...");
            var start = DateTime.UtcNow;
            while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
            {
                var ip = NetworkInfo.GetIpAddress();
                if (!string.IsNullOrEmpty(ip) && ip != "0.0.0.0")
                {
                    Debug.WriteLine($"[Net] Device IP: {ip}");
                    return;
                }
                Thread.Sleep(100);
            }
            Debug.WriteLine("[Net] Timed out waiting for IP.");
        }
    }
}
