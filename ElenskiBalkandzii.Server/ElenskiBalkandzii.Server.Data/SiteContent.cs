namespace ElenskiBalkandzii.Server.Data;

public sealed class SiteContent
{
    public int Id { get; set; }
    public string Json { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; }
}
