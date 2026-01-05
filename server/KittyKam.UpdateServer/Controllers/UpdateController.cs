using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text.Json;
using UpdateServer.Models;

namespace UpdateServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UpdateController : ControllerBase
{
    private readonly string _updatesPath = Path.Combine(Directory.GetCurrentDirectory(), "Updates");
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [HttpGet("check")]
    public async Task<IActionResult> CheckForUpdate([FromQuery] string currentVersion)
    {
        var manifestPath = Path.Combine(_updatesPath, "latest.json");
        if (!System.IO.File.Exists(manifestPath))
            return NotFound("No updates available");

        var json = await System.IO.File.ReadAllTextAsync(manifestPath);
        var manifest = JsonSerializer.Deserialize<UpdateManifest>(json, _jsonOptions);

        // Compare versions (simple string comparison, or use Version.Parse for semantic versioning)
        if (manifest?.Version == currentVersion)
            return Ok(new { updateAvailable = false, currentVersion });

        return Ok(new { updateAvailable = true, manifest });
    }

    [HttpGet("download/{version}")]
    public IActionResult DownloadFirmware(string version)
    {
        // Look for firmware file (e.g., firmware-1.0.1.bin)
        var firmwarePath = Path.Combine(_updatesPath, $"firmware-{version}.bin");
        
        if (!System.IO.File.Exists(firmwarePath))
            return NotFound($"Firmware version {version} not found");

        var fileBytes = System.IO.File.ReadAllBytes(firmwarePath);
        return File(fileBytes, "application/octet-stream", $"firmware-{version}.bin");
    }

    [HttpGet("manifest")]
    public async Task<IActionResult> GetLatestManifest()
    {
        var manifestPath = Path.Combine(_updatesPath, "latest.json");
        if (!System.IO.File.Exists(manifestPath))
            return NotFound();

        var json = await System.IO.File.ReadAllTextAsync(manifestPath);
        return Content(json, "application/json");
    }
}
