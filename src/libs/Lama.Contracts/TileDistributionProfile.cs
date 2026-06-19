namespace Lama.Contracts;

/// <summary>
/// Profil de règles utilisé pour déterminer la distribution des tuiles.
/// </summary>
public sealed record TileDistributionProfile(
    string Language,
    int BoardSize = 15,
    int RackSize = 7,
    GameLevel GameLevel = GameLevel.Standard,
    string GameType = "classic")
{
    public static TileDistributionProfile Default(string language = "fr") =>
        new(language, 15, 7, GameLevel.Standard, "classic");
}

