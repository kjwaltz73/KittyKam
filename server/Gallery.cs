using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace KittyKam.Api;

// GET /api/gallery?t=TOKEN&n=60   -> mobile-friendly photo grid, newest first
// GET /api/img?p=visitId/seq.jpg&t=TOKEN -> one photo
// Auth is a shared token in the GalleryToken app setting (separate from the device's function key).
public partial class Gallery(BlobServiceClient blobs)
{
    [GeneratedRegex(@"^\d{9,12}/\d{1,3}\.jpg$")]
    private static partial Regex PhotoPath();

    static bool Authorized(HttpRequest req)
    {
        var expected = Environment.GetEnvironmentVariable("GalleryToken");
        var given = req.Query["t"].ToString();
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(given)) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(given));
    }

    [Function("gallery")]
    public async Task<IActionResult> Page([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        if (!Authorized(req)) return new UnauthorizedResult();
        var n = int.TryParse(req.Query["n"], out var parsed) ? Math.Clamp(parsed, 1, 200) : 60;
        var t = Uri.EscapeDataString(req.Query["t"].ToString());

        var names = new List<string>();
        await foreach (var b in blobs.GetBlobContainerClient("photos").GetBlobsAsync())
            names.Add(b.Name);
        var recent = names.Where(x => PhotoPath().IsMatch(x))
            .OrderByDescending(x => long.Parse(x[..x.IndexOf('/')])).ThenByDescending(x => x)
            .Take(n).ToList();

        var sb = new StringBuilder();
        sb.Append("""
            <!doctype html><html><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <title>KittyKam</title>
            <style>
            body{margin:0;font-family:system-ui,sans-serif;background:#111;color:#eee}
            h1{font-size:1.1rem;margin:0;padding:12px 14px;background:#1c1c1c;position:sticky;top:0}
            .grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(260px,1fr));gap:8px;padding:8px}
            figure{margin:0;background:#1c1c1c;border-radius:8px;overflow:hidden}
            img{width:100%;display:block;aspect-ratio:4/3;object-fit:cover;background:#000}
            figcaption{padding:6px 10px;font-size:.85rem;color:#aaa}
            </style></head><body>
            """);
        sb.Append($"<h1>KittyKam &middot; {recent.Count} latest photos</h1><div class=\"grid\">");
        foreach (var name in recent)
        {
            var ts = name[..name.IndexOf('/')];
            var src = $"/api/img?p={Uri.EscapeDataString(name)}&t={t}";
            sb.Append($"<figure><a href=\"{src}\"><img loading=\"lazy\" src=\"{src}\"></a><figcaption data-ts=\"{ts}\">{ts}</figcaption></figure>");
        }
        sb.Append("""
            </div><script>
            document.querySelectorAll('[data-ts]').forEach(function(e){
              e.textContent=new Date(+e.dataset.ts*1000).toLocaleString([], {month:'short',day:'numeric',hour:'numeric',minute:'2-digit',second:'2-digit'});
            });
            setInterval(function(){ if(window.scrollY<50) location.reload(); }, 30000);
            </script></body></html>
            """);
        req.HttpContext.Response.Headers.CacheControl = "no-store";
        return new ContentResult { Content = sb.ToString(), ContentType = "text/html; charset=utf-8" };
    }

    [Function("img")]
    public async Task<IActionResult> Image([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        if (!Authorized(req)) return new UnauthorizedResult();
        var path = req.Query["p"].ToString();
        if (!PhotoPath().IsMatch(path)) return new BadRequestResult();

        var blob = blobs.GetBlobContainerClient("photos").GetBlobClient(path);
        if (!await blob.ExistsAsync()) return new NotFoundResult();
        req.HttpContext.Response.Headers.CacheControl = "private, max-age=86400";
        return new FileStreamResult(await blob.OpenReadAsync(), "image/jpeg");
    }
}
