using Microsoft.AspNetCore.Mvc;

namespace KittyKam.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PhotoController : ControllerBase
{
    private readonly string _photosPath = Path.Combine(Directory.GetCurrentDirectory(), "Photos");

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile photo)
    {
        if (photo == null || photo.Length == 0)
            return BadRequest("No photo provided");

        // Ensure Photos directory exists
        Directory.CreateDirectory(_photosPath);

        // Generate filename with timestamp
        var filename = $"photo_{DateTime.UtcNow:yyyyMMdd_HHmmss}.jpg";
        var filepath = Path.Combine(_photosPath, filename);

        // Save the photo
        using (var stream = System.IO.File.Create(filepath))
        {
            await photo.CopyToAsync(stream);
        }

        return Ok(new
        {
            success = true,
            filename,
            size = photo.Length,
            savedAt = DateTime.UtcNow
        });
    }

    [HttpGet("list")]
    public IActionResult ListPhotos([FromQuery] int limit = 50)
    {
        if (!Directory.Exists(_photosPath))
            return Ok(new { photos = Array.Empty<object>() });

        var files = Directory.GetFiles(_photosPath, "*.jpg")
            .OrderByDescending(f => System.IO.File.GetCreationTimeUtc(f))
            .Take(limit)
            .Select(f => new
            {
                filename = Path.GetFileName(f),
                size = new FileInfo(f).Length,
                createdUtc = System.IO.File.GetCreationTimeUtc(f)
            });

        return Ok(new { photos = files });
    }

    [HttpGet("latest")]
    public IActionResult GetLatest()
    {
        if (!Directory.Exists(_photosPath))
            return NotFound("No photos available");

        var latest = Directory.GetFiles(_photosPath, "*.jpg")
            .OrderByDescending(f => System.IO.File.GetCreationTimeUtc(f))
            .FirstOrDefault();

        if (latest == null)
            return NotFound("No photos available");

        var bytes = System.IO.File.ReadAllBytes(latest);
        return File(bytes, "image/jpeg");
    }
}
