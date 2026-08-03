using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace PersonalDevSite.Functions.Configuration;

internal static class EnvironmentConfiguration
{
  private static readonly IReadOnlyDictionary<string, string?> _appSettings = LoadAppSettings();
  private static readonly IReadOnlyDictionary<string, string?> _localSettings = LoadLocalSettings();

  private static readonly IReadOnlyDictionary<string, string> _keyVaultSecretNames =
    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
      ["AZURE_OPENAI_API_KEY"] = "openai-api-key",
      ["AZURE_SEARCH_API_KEY"] = "search-api-key"
    };

  private static SecretClient? _keyVaultClient;

  public static string GetRequired(string settingName)
  {
    var value = GetSetting(settingName);
    return string.IsNullOrWhiteSpace(value)
                ? throw new InvalidOperationException($"Set {settingName} in appsettings.json or as an environment variable before starting the Functions app.")
      : value;
  }

  public static string GetRequiredSecret(string localSettingName)
  {
    if (IsLocalDevelopment())
    {
      return RequireSecret(
        GetLocalSetting(localSettingName),
        $"Set {localSettingName} in local.settings.json or as an environment variable before starting the Functions app.");
    }

    if (!_keyVaultSecretNames.TryGetValue(localSettingName, out var keyVaultSecretName))
    {
      throw new InvalidOperationException($"No Key Vault secret mapping is configured for {localSettingName}.");
    }

    var keyVaultUri = GetSetting("AZURE_KEY_VAULT_URI");
    if (string.IsNullOrWhiteSpace(keyVaultUri))
    {
      throw new InvalidOperationException("Set AZURE_KEY_VAULT_URI in appsettings.json or as an environment variable before starting the Functions app.");
    }

    if (!Uri.TryCreate(keyVaultUri, UriKind.Absolute, out var vaultUri))
    {
      throw new InvalidOperationException("AZURE_KEY_VAULT_URI must be an absolute Key Vault URI.");
    }

    _keyVaultClient ??= new SecretClient(vaultUri, new DefaultAzureCredential());

    try
    {
      var secret = _keyVaultClient.GetSecret(keyVaultSecretName).Value.Value;
      return RequireSecret(secret, $"Key Vault secret '{keyVaultSecretName}' must have a value.");
    }
    catch (RequestFailedException exception) when (exception.Status == 404)
    {
      throw new InvalidOperationException(
        $"Key Vault secret '{keyVaultSecretName}' was not found in {vaultUri}.",
        exception);
    }
  }

  private static string RequireSecret(string? value, string errorMessage)
  {
    return string.IsNullOrWhiteSpace(value)
      ? throw new InvalidOperationException(errorMessage)
      : value;
  }

  private static bool IsLocalDevelopment()
  {
    var environmentName = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT")
      ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
      ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

    if (!string.IsNullOrWhiteSpace(environmentName))
    {
      return string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);
    }

    var workerHostEndpoint = Environment.GetEnvironmentVariable("FUNCTIONS_WORKER_HOST_ENDPOINT");
    if (Uri.TryCreate(workerHostEndpoint, UriKind.Absolute, out var workerUri)
      && (string.Equals(workerUri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
        || string.Equals(workerUri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(workerUri.Host, "::1", StringComparison.OrdinalIgnoreCase)))
    {
      return true;
    }

    return FindSettingsPath("local.settings.json") is not null;
  }

  private static string? GetSetting(string settingName)
  {
    var environmentValue = Environment.GetEnvironmentVariable(settingName);
    if (!string.IsNullOrWhiteSpace(environmentValue))
    {
      return environmentValue;
    }

    return _localSettings.GetValueOrDefault(settingName) ?? _appSettings.GetValueOrDefault(settingName);
  }

  private static string? GetLocalSetting(string settingName)
  {
    var environmentValue = Environment.GetEnvironmentVariable(settingName);
    return string.IsNullOrWhiteSpace(environmentValue)
      ? _localSettings.GetValueOrDefault(settingName)
      : environmentValue;
  }

  private static Dictionary<string, string?> LoadAppSettings()
  {
    var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    if (!File.Exists(settingsPath))
    {
      return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    }

    using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
    return document.RootElement.EnumerateObject()
      .Where(property => property.Value.ValueKind == JsonValueKind.String)
      .ToDictionary(property => property.Name, property => property.Value.GetString(), StringComparer.OrdinalIgnoreCase);
  }

  private static Dictionary<string, string?> LoadLocalSettings()
  {
    var settingsPath = FindSettingsPath("local.settings.json");
    if (settingsPath is null)
    {
      return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    }

    using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
    if (!document.RootElement.TryGetProperty("Values", out var values)
      || values.ValueKind != JsonValueKind.Object)
    {
      return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    }

    return values.EnumerateObject()
      .Where(property => property.Value.ValueKind == JsonValueKind.String)
      .ToDictionary(property => property.Name, property => property.Value.GetString(), StringComparer.OrdinalIgnoreCase);
  }

  private static string? FindSettingsPath(string fileName)
  {
    return new[]
      {
        Path.Combine(AppContext.BaseDirectory, fileName),
        Path.Combine(Directory.GetCurrentDirectory(), fileName)
      }
      .FirstOrDefault(File.Exists);
  }
}
