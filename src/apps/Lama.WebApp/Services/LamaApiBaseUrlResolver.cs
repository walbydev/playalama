using Microsoft.Extensions.Configuration;

namespace Lama.WebApp.Services;

public static class LamaApiBaseUrlResolver
{
    private const int ForbiddenLegacyPort = 5000;
    private const int LocalServerPort = 5201;
    private const string LocalFallbackUrl = "http://127.0.0.1:5201";

    public static string Resolve(IConfiguration configuration)
    {
        var configured = configuration["LAMA_SERVER_URL"]
            ?? configuration["LamaApi:BaseUrl"]
            ?? LocalFallbackUrl;

        if (!Uri.TryCreate(configured, UriKind.Absolute, out var uri))
            return LocalFallbackUrl;

        if (uri.Port != ForbiddenLegacyPort)
            return uri.ToString().TrimEnd('/');

        var remapped = new UriBuilder(uri)
        {
            Port = LocalServerPort
        };
        return remapped.Uri.ToString().TrimEnd('/');
    }
}
