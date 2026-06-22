using System.Security.Claims;

namespace Lama.Server.Endpoints;

/// <summary>
/// Extensions pour vérifier l'authentification JWT sur les endpoints.
/// </summary>
public static class AuthorizationExtensions
{
    /// <summary>
    /// Retourne vrai si l'utilisateur est authentifié via JWT.
    /// </summary>
    public static bool IsAuthenticated(this HttpContext context)
    {
        return context.User?.Identity?.IsAuthenticated == true &&
               context.User.FindFirst("playerId") != null;
    }

    /// <summary>
    /// Retourne le PlayerId du contexte d'authentification.
    /// </summary>
    public static string? GetPlayerId(this HttpContext context)
    {
        return context.User?.FindFirst("playerId")?.Value;
    }

    /// <summary>
    /// Retourne le PlayerName du contexte d'authentification.
    /// </summary>
    public static string? GetPlayerName(this HttpContext context)
    {
        return context.User?.FindFirst(ClaimTypes.Name)?.Value;
    }

    /// <summary>
    /// Retourne un résultat Unauthorized si l'utilisateur n'est pas authentifié.
    /// </summary>
    public static IResult RequireAuth(this HttpContext context)
    {
        if (!context.IsAuthenticated())
            return Results.Unauthorized();

        return Results.Empty;
    }
}

