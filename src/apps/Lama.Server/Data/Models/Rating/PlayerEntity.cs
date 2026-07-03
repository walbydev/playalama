namespace Lama.Server.Data.Models.Rating;

public sealed class PlayerEntity
{
    public Guid PlayerId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PasswordHash { get; set; }
    public string? CountryCode { get; set; }
    public string? AccessibilityPreferencesJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<PlayerRatingEntity> Ratings { get; set; } = [];
}
