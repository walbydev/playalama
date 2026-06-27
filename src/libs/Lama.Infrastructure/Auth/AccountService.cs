using System.Text.Json;
using System.Text.Json.Serialization;
using Lama.Contracts;
using Microsoft.Extensions.Logging;

namespace Lama.Infrastructure.Auth;

/// <summary>
/// Implémentation de <see cref="IAccountService"/> basée sur un fichier JSON local.
///
/// Les comptes sont persistés dans <c>accounts.json</c>, dans le même répertoire
/// que <c>session.json</c> (résolu cross-platform via
/// <see cref="Environment.SpecialFolder.ApplicationData"/>).
///
/// Chemin typique :
/// <list type="bullet">
///   <item>Linux   : <c>~/.config/lama/accounts.json</c></item>
///   <item>Windows : <c>%APPDATA%\lama\accounts.json</c></item>
///   <item>macOS   : <c>~/Library/Application Support/lama/accounts.json</c></item>
/// </list>
///
/// La variable d'environnement <c>LAMA_SESSION_DIR</c> surcharge le répertoire
/// (cohérent avec <c>SessionService</c>).
/// </summary>
public sealed class AccountService : IAccountService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string _accountsFilePath;
    private readonly ILogger<AccountService> _logger;

    /// <summary>Initialise le service.</summary>
    public AccountService(ILogger<AccountService> logger)
    {
        _logger = logger;
        _accountsFilePath = ResolveAccountsFilePath();
    }

    /// <inheritdoc />
    public bool IsInitialized =>
        File.Exists(_accountsFilePath) &&
        LoadAccounts().Any(a => a.Role == Role.SuperAdmin && a.Active);

    /// <inheritdoc />
    public Account CreateSuperAdmin(string username, string password)
    {
        var accounts = LoadAccounts();
        if (accounts.Any(a => a.Role == Role.SuperAdmin))
            throw new InvalidOperationException(
                "Un compte SuperAdmin existe déjà. " +
                "Utilisez 'lama system account reset-password' pour changer le mot de passe.");

        return CreateAndSave(username, password, Role.SuperAdmin, accounts);
    }

    /// <inheritdoc />
    public Account CreateAdmin(string username, string password)
    {
        var accounts = LoadAccounts();
        if (accounts.Any(a => a.Username.Equals(username, StringComparison.OrdinalIgnoreCase) && a.Active))
            throw new InvalidOperationException(
                $"Un compte actif avec le nom '{username}' existe déjà.");

        return CreateAndSave(username, password, Role.Admin, accounts);
    }

    /// <inheritdoc />
    public IReadOnlyList<Account> GetAll() => LoadAccounts();

    /// <inheritdoc />
    public Account? FindByUsername(string username) =>
        LoadAccounts().FirstOrDefault(a =>
            a.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public bool Revoke(string username)
    {
        var accounts = LoadAccounts();
        var account  = accounts.FirstOrDefault(a =>
            a.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

        if (account is null) return false;

        if (account.Role == Role.SuperAdmin)
            throw new InvalidOperationException(
                "Le compte SuperAdmin ne peut pas être révoqué.");

        var updated = accounts
            .Select(a => a.Username.Equals(username, StringComparison.OrdinalIgnoreCase)
                ? a with { Active = false }
                : a)
            .ToList();

        SaveAccounts(updated);
        _logger.LogInformation("Compte révoqué : {Username}", username);
        return true;
    }

    /// <inheritdoc />
    public bool ResetPassword(string username, string newPassword)
    {
        var accounts = LoadAccounts();
        var account  = accounts.FirstOrDefault(a =>
            a.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

        if (account is null) return false;

        var salt        = PasswordHasher.GenerateSalt();
        var hash        = PasswordHasher.Hash(newPassword, salt);
        var updated = accounts
            .Select(a => a.Username.Equals(username, StringComparison.OrdinalIgnoreCase)
                ? a with { PasswordHash = hash, Salt = salt }
                : a)
            .ToList();

        SaveAccounts(updated);
        _logger.LogInformation("Mot de passe réinitialisé : {Username}", username);
        return true;
    }

    /// <inheritdoc />
    public bool VerifyPassword(Account account, string password) =>
        PasswordHasher.Verify(password, account.Salt, account.PasswordHash);

    // ─── Helpers privés ──────────────────────────────────────────────────────

    private Account CreateAndSave(
        string username, string password, Role role, List<Account> existingAccounts)
    {
        var salt    = PasswordHasher.GenerateSalt();
        var hash    = PasswordHasher.Hash(password, salt);
        var account = new Account(
            Id:           Guid.NewGuid().ToString("N"),
            Username:     username,
            Role:         role,
            PasswordHash: hash,
            Salt:         salt,
            CreatedAt:    DateTimeOffset.UtcNow,
            Active:       true);

        existingAccounts.Add(account);
        SaveAccounts(existingAccounts);

        _logger.LogInformation("Compte créé : {Username} ({Role})", username, role);
        return account;
    }

    private List<Account> LoadAccounts()
    {
        if (!File.Exists(_accountsFilePath))
            return [];

        try
        {
            var json = File.ReadAllText(_accountsFilePath);
            return JsonSerializer.Deserialize<List<Account>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _logger.LogWarning(ex, "Impossible de lire accounts.json : {Path}", _accountsFilePath);
            return [];
        }
    }

    private void SaveAccounts(List<Account> accounts)
    {
        var dir = Path.GetDirectoryName(_accountsFilePath)!;
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(accounts, JsonOptions);
        File.WriteAllText(_accountsFilePath, json);
    }

    private static string ResolveAccountsFilePath()
    {
        var envDir = Environment.GetEnvironmentVariable("LAMA_SESSION_DIR");
        if (!string.IsNullOrWhiteSpace(envDir))
            return Path.Combine(envDir, "accounts.json");

        var appData = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData,
            Environment.SpecialFolderOption.Create);

        return Path.Combine(appData, "lama", "accounts.json");
    }

}
