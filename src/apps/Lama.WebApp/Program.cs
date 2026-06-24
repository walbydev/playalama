using Lama.WebApp.Components;
using Lama.WebApp.Services;
using Lama.WebApp.ViewModels;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient<LamaApiClient>(client =>
{
    var baseUrl = builder.Configuration["LamaApi:BaseUrl"]
        ?? Environment.GetEnvironmentVariable("LAMA_SERVER_URL")
        ?? "http://127.0.0.1:5000";
    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
});

// Services scoped par circuit Blazor (une instance par connexion navigateur)
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ThemeService>();

// ViewModels (MVVM léger)
builder.Services.AddScoped<HomeViewModel>();
builder.Services.AddScoped<LeaderboardViewModel>();
builder.Services.AddScoped<DownloadsViewModel>();
builder.Services.AddScoped<RulesViewModel>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
