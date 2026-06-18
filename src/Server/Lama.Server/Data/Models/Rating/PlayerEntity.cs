namespace Lama.Server.Data.Models.Rating;

public sealed class PlayerEntity
{
    public Guid PlayerId { get; set; }
    public string Username { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<PlayerRatingEntity> Ratings { get; set; } = [];
}

