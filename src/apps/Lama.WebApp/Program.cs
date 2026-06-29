using Lama.WebApp.Components;
using Lama.WebApp.Services;
using Lama.WebApp.ViewModels;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

var supportedCultures = new[] { "fr", "en", "de" };
builder.Services.Configure<Microsoft.AspNetCore.Builder.RequestLocalizationOptions>(options =>
{
    options.SetDefaultCulture("fr");
    options.AddSupportedCultures(supportedCultures);
    options.AddSupportedUICultures(supportedCultures);
});

builder.Services.AddHttpClient<LamaApiClient>(client =>
{
    var baseUrl = LamaApiBaseUrlResolver.Resolve(builder.Configuration);
    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
});

// Services scoped par circuit Blazor (une instance par connexion navigateur)
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<BoardZoomService>();
builder.Services.AddScoped<LanguageService>();

// ViewModels (MVVM léger)
builder.Services.AddScoped<HomeViewModel>();
builder.Services.AddScoped<LeaderboardViewModel>();
builder.Services.AddScoped<DownloadsViewModel>();
builder.Services.AddScoped<RulesViewModel>();
builder.Services.AddScoped<StatusViewModel>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRequestLocalization();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
