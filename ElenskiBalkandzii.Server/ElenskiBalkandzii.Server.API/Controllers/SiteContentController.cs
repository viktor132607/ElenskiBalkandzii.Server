using System.Text.Json;
using ElenskiBalkandzii.Server.API;
using ElenskiBalkandzii.Server.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;

namespace ElenskiBalkandzii.Server.API.Controllers;

[ApiController]
[Route("api")]
public sealed class SiteContentController(ApplicationDbContext db, AdminAccess access) : ControllerBase
{
    [HttpGet("content")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        string? json = await db.SiteContents.AsNoTracking().Where(x => x.Id == 1)
            .Select(x => x.Json).SingleOrDefaultAsync(cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return json is null ? NoContent() : Content(json, "application/json");
    }

    [HttpPost("admin/login")]
    [EnableRateLimiting("admin-login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        Response.Headers.CacheControl = "no-store";
        if (!access.IsConfigured) return StatusCode(503, new { error = "Admin authentication is not configured." });
        if (!access.CheckPassword(request.Password)) return Unauthorized(new { error = "Invalid password." });
        return Ok(new { token = access.IssueToken() });
    }

    [HttpGet("admin/session")]
    public IActionResult Session() => Authorized() ? NoContent() : Unauthorized();

    [HttpPut("admin/content")]
    [RequestSizeLimit(131072)]
    public async Task<IActionResult> Save([FromBody] JsonElement content, CancellationToken cancellationToken)
    {
        if (!Authorized()) return Unauthorized();
        string json = content.GetRawText();
        if (json.Length > 120000 || !ValidContent(content)) return BadRequest(new { error = "Invalid content." });

        SiteContent? existing = await db.SiteContents.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (existing is null) db.SiteContents.Add(new SiteContent { Id = 1, Json = json, UpdatedAt = DateTime.UtcNow });
        else { existing.Json = json; existing.UpdatedAt = DateTime.UtcNow; }
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private bool Authorized() => access.ValidToken(Request.Headers.Authorization.ToString());

    private static bool ValidContent(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return false;
        if (!root.TryGetProperty("media", out JsonElement media) || !HasStrings(media, "logo", "store", "products")) return false;
        foreach (string key in new[] { "logo", "store", "products" })
        {
            string path = media.GetProperty(key).GetString()!;
            if (!(path.StartsWith("/api/images/", StringComparison.Ordinal) ||
                  path.StartsWith("/elenski-balkandzhii-", StringComparison.Ordinal) ||
                  path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))) return false;
        }
        foreach (string locale in new[] { "bg", "en" })
        {
            if (!root.TryGetProperty(locale, out JsonElement section) || section.ValueKind != JsonValueKind.Object) return false;
            foreach (string name in new[] { "home", "about", "contact", "products" })
                if (!section.TryGetProperty(name, out JsonElement item) || item.ValueKind != JsonValueKind.Object) return false;
            JsonElement home = section.GetProperty("home");
            if (!HasStrings(home, "eyebrow", "title", "view")) return false;
            JsonElement about = section.GetProperty("about");
            if (!HasStrings(about, "eyebrow", "title", "copy") || !about.TryGetProperty("rows", out JsonElement rows) ||
                rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() > 20 ||
                rows.EnumerateArray().Any(x => !HasStrings(x, "label", "copy"))) return false;
            JsonElement contact = section.GetProperty("contact");
            if (!HasStrings(contact, "heading", "address", "phone", "note") ||
                !contact.TryGetProperty("hours", out JsonElement hours) || hours.ValueKind != JsonValueKind.Array ||
                hours.GetArrayLength() != 7 || hours.EnumerateArray().Any(x => !HasStrings(x, "day", "hours"))) return false;
            JsonElement products = section.GetProperty("products");
            if (!products.TryGetProperty("categories", out JsonElement categories) || categories.ValueKind != JsonValueKind.Array || categories.GetArrayLength() != 3) return false;
            foreach (JsonElement category in categories.EnumerateArray())
            {
                if (!HasStrings(category, "title") ||
                    !category.TryGetProperty("items", out JsonElement items) || items.ValueKind != JsonValueKind.Array || items.GetArrayLength() > 40 ||
                    items.EnumerateArray().Any(x => x.ValueKind != JsonValueKind.String || x.GetString()!.Length > 5000)) return false;
            }
        }
        return true;
    }

    private static bool HasStrings(JsonElement element, params string[] names) =>
        element.ValueKind == JsonValueKind.Object && names.All(name =>
            element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String && value.GetString()!.Length <= 5000);
}

public sealed record LoginRequest(string? Password);
