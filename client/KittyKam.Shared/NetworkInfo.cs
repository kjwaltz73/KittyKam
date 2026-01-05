
// NetworkInfo.cs (nanoFramework-friendly)
using System.Net;
using System.Net.NetworkInformation;
using System.Diagnostics;

namespace KittyKamHost
{
    public static class NetworkInfo
    {
        public static string GetIpAddress()
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var ni in interfaces)
                {
                    // nanoFramework exposes IPv4 fields directly
                    var ip = ni.IPv4Address;
                    if (!string.IsNullOrEmpty(ip) && ip != "0.0.0.0")
                    {
                        return ip;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine("[Net] GetIpAddress error: " + ex.Message);
            }

            return "0.0.0.0";
        }
    }
}
