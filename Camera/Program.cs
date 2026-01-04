using System;
using System.Threading;

namespace KittyKamHost
{
    public class Program
    {
        public static void Main()
        {
            // TODO: set your Wi‑Fi SSID/password here
            Wifi.Connect("KYLEW-24", "@[DrunkF!sh]@");
            
            WaitForIp();

            // Initialize host status
            OtaHost.Init("1.0.0");

            // Start admin web server on port 80
            var admin = new AdminServer();
            admin.Start(80);

            // Keep running
            Thread.Sleep(Timeout.Infinite);
        }

        // Program.cs (add before starting AdminServer)
        private static void WaitForIp(int timeoutMs = 15000)
        {
            var start = System.DateTime.UtcNow;
            while ((System.DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
            {
                var ip = NetworkInfo.GetIpAddress();
                if (!string.IsNullOrEmpty(ip) && ip != "0.0.0.0")
                {
                    System.Diagnostics.Debug.WriteLine($"[Net] Device IP: {ip}");
                    return;
                }
                // brief yield
            }
            System.Diagnostics.Debug.WriteLine("[Net] Timed out waiting for IP.");
        }

    }
}
