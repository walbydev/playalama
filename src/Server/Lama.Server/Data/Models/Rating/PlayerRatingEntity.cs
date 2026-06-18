namespace Lama.Server.Data.Models.Rating;

public sealed class PlayerRatingEntity
{
    public Guid RatingRecordId { get; set; }
    public Guid PlayerId { get; set; }
    public string Queue { get; set; } = "open";
    public decimal EloRating { get; set; } = 1400m;
    public int GamesPlayed { get; set; }
    public int GamesWon { get; set; }
    public int GamesLost { get; set; }
    public int GamesAbandoned { get; set; }
    public int TotalPoints { get; set; }
    public decimal? AvgScore { get; set; }
    public DateTimeOffset? LastGameDate { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public PlayerEntity Player { get; set; } = null!;
}

