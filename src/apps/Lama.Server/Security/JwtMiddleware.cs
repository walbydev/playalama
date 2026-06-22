using System.Security.Claims;

namespace Lama.Server.Security;

/// <summary>
/// Middleware qui valide les tokens JWT dans les requêtes.
/// Ajoute les claims aux HttpContext si la authentification est présente.
/// </summary>
public sealed class JwtMiddleware
{
    private readonly RequestDelegate _next;
    private readonly JwtTokenService _tokenService;

    public JwtMiddleware(RequestDelegate next, JwtTokenService tokenService)
    {
        _next = next;
        _tokenService = tokenService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader.Substring("Bearer ".Length).Trim();
            var principal = _tokenService.ValidateToken(token);

            if (principal != null)
            {
                context.User = principal;
            }
        }

        await _next(context);
    }
}

/// <summary>
/// Extensions pour enregistrer le middleware JWT.
/// </summary>
public static class JwtMiddlewareExtensions
{
    public static IApplicationBuilder UseJwtMiddleware(
        this IApplicationBuilder builder,
        JwtTokenService tokenService)
    {
        return builder.UseMiddleware<JwtMiddleware>(tokenService);
    }
}

