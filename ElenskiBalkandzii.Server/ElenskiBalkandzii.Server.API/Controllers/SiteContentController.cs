using System.Text.Json;
using System.Globalization;
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
        if (root.TryGetProperty("mediaPlacements", out JsonElement mediaPlacements))
        {
            if (mediaPlacements.ValueKind != JsonValueKind.Object) return false;
            foreach (JsonProperty property in mediaPlacements.EnumerateObject())
                if (property.Name is not ("logo" or "store" or "products" or "about") || !ValidPlacement(property.Value)) return false;
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
                hours.GetArrayLength() != 7 || hours.EnumerateArray().Any(x => !HasStrings(x, "day", "hours")) ||
                !ValidOptionalString(contact, "phone2") || !ValidOptionalString(contact, "email") ||
                !ValidSpecialHours(contact)) return false;
            JsonElement products = section.GetProperty("products");
            if (!products.TryGetProperty("categories", out JsonElement categories) || !ValidCategories(categories)) return false;
        }
        JsonElement bgCategories = root.GetProperty("bg").GetProperty("products").GetProperty("categories");
        JsonElement enCategories = root.GetProperty("en").GetProperty("products").GetProperty("categories");
        if (bgCategories.GetArrayLength() != enCategories.GetArrayLength()) return false;
        for (int i = 0; i < bgCategories.GetArrayLength(); i++)
        {
            JsonElement bgCategory = bgCategories[i];
            JsonElement enCategory = enCategories[i];
            if (bgCategory.GetProperty("id").GetString() != enCategory.GetProperty("id").GetString()) return false;
            JsonElement bgItems = bgCategory.GetProperty("items");
            JsonElement enItems = enCategory.GetProperty("items");
            if (bgItems.GetArrayLength() != enItems.GetArrayLength()) return false;
            for (int j = 0; j < bgItems.GetArrayLength(); j++)
                if (bgItems[j].GetProperty("id").GetString() != enItems[j].GetProperty("id").GetString()) return false;
        }
        JsonElement bgContact = root.GetProperty("bg").GetProperty("contact");
        JsonElement enContact = root.GetProperty("en").GetProperty("contact");
        int bgCount = bgContact.TryGetProperty("specialHours", out JsonElement bgDates) ? bgDates.GetArrayLength() : 0;
        int enCount = enContact.TryGetProperty("specialHours", out JsonElement enDates) ? enDates.GetArrayLength() : 0;
        if (bgCount != enCount) return false;
        for (int i = 0; i < bgCount; i++)
        {
            if (bgDates[i].GetProperty("id").GetString() != enDates[i].GetProperty("id").GetString() ||
                bgDates[i].GetProperty("date").GetString() != enDates[i].GetProperty("date").GetString()) return false;
        }
        return true;
    }

    private static bool ValidOptionalString(JsonElement element, string name) =>
        !element.TryGetProperty(name, out JsonElement value) || value.ValueKind == JsonValueKind.String && value.GetString()!.Length <= 5000;

    private static bool ValidSpecialHours(JsonElement contact)
    {
        if (!contact.TryGetProperty("specialHours", out JsonElement rows)) return true;
        if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() > 30) return false;
        var ids = new HashSet<string>();
        var dates = new HashSet<string>();
        foreach (JsonElement row in rows.EnumerateArray())
        {
            if (!HasStrings(row, "id", "date", "label", "hours")) return false;
            string id = row.GetProperty("id").GetString()!;
            string date = row.GetProperty("date").GetString()!;
            if (!ValidId(id) || !ids.Add(id) || !dates.Add(date) ||
                !DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ||
                string.IsNullOrWhiteSpace(row.GetProperty("label").GetString()) ||
                string.IsNullOrWhiteSpace(row.GetProperty("hours").GetString())) return false;
        }
        return true;
    }

    private static bool ValidCategories(JsonElement categories)
    {
        if (categories.ValueKind != JsonValueKind.Array || categories.GetArrayLength() > 24) return false;
        var categoryIds = new HashSet<string>();
        foreach (JsonElement category in categories.EnumerateArray())
        {
            if (!HasStrings(category, "id", "title", "description", "image") ||
                !ValidId(category.GetProperty("id").GetString()!) ||
                !categoryIds.Add(category.GetProperty("id").GetString()!) ||
                !ValidImage(category.GetProperty("image").GetString()!) ||
                !category.TryGetProperty("visible", out JsonElement visible) || !IsBoolean(visible) ||
                !category.TryGetProperty("items", out JsonElement items) || items.ValueKind != JsonValueKind.Array || items.GetArrayLength() > 40) return false;
            if (category.TryGetProperty("imagePlacement", out JsonElement categoryPlacement) && !ValidPlacement(categoryPlacement)) return false;
            var productIds = new HashSet<string>();
            foreach (JsonElement product in items.EnumerateArray())
            {
                if (!HasStrings(product, "id", "title", "description", "image") ||
                    !ValidId(product.GetProperty("id").GetString()!) ||
                    !productIds.Add(product.GetProperty("id").GetString()!) ||
                    !ValidImage(product.GetProperty("image").GetString()!) ||
                    !product.TryGetProperty("visible", out JsonElement productVisible) || !IsBoolean(productVisible)) return false;
                if (product.TryGetProperty("imagePlacement", out JsonElement productPlacement) && !ValidPlacement(productPlacement)) return false;
            }
        }
        return true;
    }

    private static bool IsBoolean(JsonElement value) => value.ValueKind is JsonValueKind.True or JsonValueKind.False;
    private static bool ValidPlacement(JsonElement placement)
    {
        if (placement.ValueKind != JsonValueKind.Object || !placement.TryGetProperty("fit", out JsonElement fit) ||
            fit.ValueKind != JsonValueKind.String || fit.GetString() is not ("cover" or "contain")) return false;
        foreach (string key in new[] { "x", "y" })
            if (!placement.TryGetProperty(key, out JsonElement coordinate) || coordinate.ValueKind != JsonValueKind.Number ||
                !coordinate.TryGetDouble(out double value) || !double.IsFinite(value) || value < 0 || value > 100) return false;
        return !placement.TryGetProperty("zoom", out JsonElement zoom) ||
            zoom.ValueKind == JsonValueKind.Number && zoom.TryGetDouble(out double scale) && double.IsFinite(scale) && scale is >= 1 and <= 3;
    }
    private static bool ValidId(string id) => id.Length is > 0 and <= 90 && id.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_');
    private static bool ValidImage(string value) => value.Length == 0 || value.StartsWith("/api/images/", StringComparison.Ordinal) ||
        value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    private static bool HasStrings(JsonElement element, params string[] names) =>
        element.ValueKind == JsonValueKind.Object && names.All(name =>
            element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String && value.GetString()!.Length <= 5000);
}

public sealed record LoginRequest(string? Password);
