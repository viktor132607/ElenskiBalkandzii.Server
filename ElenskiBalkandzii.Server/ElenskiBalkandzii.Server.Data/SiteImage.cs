namespace ElenskiBalkandzii.Server.Data;

public sealed class SiteImage
{
    public Guid Id { get; set; }
    public string ContentType { get; set; } = "image/jpeg";
    public byte[] Data { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}
