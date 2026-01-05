using System;
using System.Diagnostics;
using nanoFramework.Hardware.Esp32;
using nanoFramework.Networking;
using System.Net.Http;

namespace KittyKam.Bootloader
{
    public static class OtaUpdater
    {
        public static string CurrentVersion { get; private set; } = "0.0.0";
        public static string LastResult { get; private set; } = "n/a";
        public static DateTime LastCheckUtc { get; private set; } = DateTime.MinValue;

        private static string _updateServerUrl = "http://192.168.1.100:5000"; // TODO: Change to your PC IP

        public static void Init(string version) => CurrentVersion = version;

        public static void CheckForUpdate(bool manual)
        {
            LastCheckUtc = DateTime.UtcNow;
            Debug.WriteLine($"[OTA] Checking for updates... Current: {CurrentVersion}");

            try
            {
                // Step 1: Check if update available
                var checkUrl = $"{_updateServerUrl}/api/update/check?currentVersion={CurrentVersion}";
                Debug.WriteLine($"[OTA] Requesting: {checkUrl}");

                using var client = new HttpClient();
                using var response = client.Get(checkUrl);

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    LastResult = $"Server returned {response.StatusCode}";
                    Debug.WriteLine($"[OTA] {LastResult}");
                    return;
                }

                var json = response.Content.ReadAsString();
                Debug.WriteLine($"[OTA] Response: {json}");

                // Simple JSON parsing (nanoFramework doesn't have full JSON support on all devices)
                var updateAvailable = json.Contains("\"updateAvailable\":true") || json.Contains("\"updateAvailable\": true");

                if (!updateAvailable)
                {
                    LastResult = "Up to date";
                    Debug.WriteLine("[OTA] Already on latest version");
                    return;
                }

                // Extract version from JSON (simple string parsing)
                var versionStart = json.IndexOf("\"version\":\"") + 11;
                if (versionStart < 11) versionStart = json.IndexOf("\"version\": \"") + 12;
                var versionEnd = json.IndexOf("\"", versionStart);
                var newVersion = json.Substring(versionStart, versionEnd - versionStart);

                // Extract download URL
                var urlStart = json.IndexOf("\"downloadUrl\":\"") + 15;
                if (urlStart < 15) urlStart = json.IndexOf("\"downloadUrl\": \"") + 16;
                var urlEnd = json.IndexOf("\"", urlStart);
                var downloadUrl = json.Substring(urlStart, urlEnd - urlStart);

                Debug.WriteLine($"[OTA] New version available: {newVersion}");
                Debug.WriteLine($"[OTA] Downloading from: {downloadUrl}");

                // Step 2: Download firmware
                using var downloadResponse = client.Get(downloadUrl);
                if (downloadResponse.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    LastResult = $"Download failed: {downloadResponse.StatusCode}";
                    Debug.WriteLine($"[OTA] {LastResult}");
                    return;
                }

                var firmwareData = downloadResponse.Content.ReadAsByteArray();
                Debug.WriteLine($"[OTA] Downloaded {firmwareData.Length} bytes");

                // Step 3: Install firmware update
                Debug.WriteLine("[OTA] Installing update...");
                // Note: nanoFramework ESP32 doesn't have built-in OTA update methods
                // You'll need to implement this using ESP32-specific APIs or partition management
                // This is a placeholder - actual implementation depends on your bootloader strategy
                
                LastResult = $"Update to v{newVersion} downloaded. Manual flash required.";
                Debug.WriteLine($"[OTA] {LastResult}");
                
                // For now, just save the firmware data or trigger a manual update process
                // Uncomment when you have the proper ESP32 OTA implementation:
                // EspNativeOta.Begin();
                // EspNativeOta.Write(firmwareData);
                // EspNativeOta.End();

                // Wait a moment for message to be sent
                System.Threading.Thread.Sleep(1000);
                
                // Reboot using ESP32 deep sleep wake (alternative to Power.RebootDevice)
                // Sleep.EnableWakeupByTimer(TimeSpan.FromMilliseconds(100));
                // Sleep.StartDeepSleep();
            }
            catch (Exception ex)
            {
                LastResult = $"Error: {ex.Message}";
                Debug.WriteLine($"[OTA] Exception: {ex}");
            }
        }
    }
}