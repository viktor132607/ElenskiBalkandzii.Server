using ElenskiBalkandzii.Server.API;
using ElenskiBalkandzii.Server.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ElenskiBalkandzii.Server.API.Controllers;

[ApiController]
[Route("api/images")]
public sealed class SiteImagesController(ApplicationDbContext db, AdminAccess access) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        SiteImage? image = await db.SiteImages.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return image is null ? NotFound() : File(image.Data, image.ContentType);
    }

    [HttpPost]
    [RequestSizeLimit(5_300_000)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken cancellationToken)
    {
        if (!access.ValidToken(Request.Headers.Authorization.ToString())) return Unauthorized();
        if (file is null || file.Length < 12 || file.Length > 5_000_000) return BadRequest(new { error = "File must be an image up to 5 MB." });
        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        byte[] bytes = buffer.ToArray();
        string? type = DetectType(bytes);
        if (type is null) return BadRequest(new { error = "Only JPEG, PNG and WebP images are supported." });
        var image = new SiteImage { Id = Guid.NewGuid(), ContentType = type, Data = bytes, CreatedAt = DateTime.UtcNow };
        db.SiteImages.Add(image);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { url = $"/api/images/{image.Id}" });
    }

    private static string? DetectType(byte[] bytes)
    {
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return "image/jpeg";
        if (bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }
}
