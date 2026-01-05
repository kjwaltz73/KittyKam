using System;

namespace KittyKam.Shared
{
    /// <summary>
    /// Provides status information for the AdminServer /status endpoint.
    /// Implemented by Bootloader and Firmware to supply their specific status data.
    /// </summary>
    public interface IStatusProvider
    {
        /// <summary>
        /// Gets the current version/build identifier.
        /// </summary>
        string GetVersion();

        /// <summary>
        /// Gets additional status information as key-value pairs for the JSON response.
        /// Examples: "lastCheck", "lastResult", "lastPhoto", "queueDepth", etc.
        /// </summary>
        string GetStatusJson();
    }
}
