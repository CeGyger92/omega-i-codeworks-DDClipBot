using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using DDClipBot.Host.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DDClipBot.Host.Services;

public class YouTubeTokenRefreshService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly IOptionsMonitor<YouTubeOptions> _youtubeOptions;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<YouTubeTokenRefreshService> _logger;
    private readonly TimeSpan _refreshInterval = TimeSpan.FromHours(24);

    public YouTubeTokenRefreshService(
        IConfiguration configuration,
        IOptionsMonitor<YouTubeOptions> youtubeOptions,
        IHttpClientFactory httpClientFactory,
        ILogger<YouTubeTokenRefreshService> logger)
    {
        _configuration = configuration;
        _youtubeOptions = youtubeOptions;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("YouTube Token Refresh Service started");

        // Wait 1 hour after startup before first refresh (let app stabilize)
        await Task.Delay(TimeSpan.FromHours(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshTokenAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing YouTube token");
            }

            // Wait 24 hours before next refresh
            _logger.LogInformation("Next YouTube token refresh in {Hours} hours", _refreshInterval.TotalHours);
            await Task.Delay(_refreshInterval, stoppingToken);
        }

        _logger.LogInformation("YouTube Token Refresh Service stopped");
    }

    private async Task RefreshTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            var options = _youtubeOptions.CurrentValue;

            if (string.IsNullOrEmpty(options.RefreshToken))
            {
                _logger.LogWarning("No refresh token configured, skipping token refresh");
                return;
            }

            _logger.LogInformation("Refreshing YouTube access token...");

            var httpClient = _httpClientFactory.CreateClient();

            var request = new RefreshTokenRequest
            {
                ClientId = options.ClientId,
                ClientSecret = options.ClientSecret,
                RefreshToken = options.RefreshToken,
                GrantType = "refresh_token"
            };

            var response = await httpClient.PostAsJsonAsync(
                "https://oauth2.googleapis.com/token",
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to refresh YouTube token. Status: {Status}, Error: {Error}",
                    response.StatusCode, errorContent);
                return;
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<RefreshTokenResponse>(
                cancellationToken: cancellationToken);

            if (tokenResponse == null)
            {
                _logger.LogError("Failed to parse token response");
                return;
            }

            _logger.LogInformation("Successfully refreshed YouTube access token. Expires in {ExpiresIn} seconds",
                tokenResponse.ExpiresIn);

            // If a new refresh token is provided, update configuration
            if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
            {
                _logger.LogInformation("New refresh token received from Google. Updating configuration...");
                await UpdateRefreshTokenAsync(tokenResponse.RefreshToken, cancellationToken);
            }
            else
            {
                _logger.LogInformation("No new refresh token provided (existing token still valid)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while refreshing YouTube token");
            throw;
        }
    }

    private async Task UpdateRefreshTokenAsync(string newRefreshToken, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("New refresh token received from Google");

            // For a hobby app, just log the new token
            // You can manually update it in Azure Portal or restart is needed anyway
            _logger.LogWarning(
                "New YouTube refresh token available. Current token will remain valid, but you can update if needed:\n" +
                "Azure Portal: App Service -> Configuration -> YouTube__RefreshToken\n" +
                "Local Dev: dotnet user-secrets set \"YouTube:RefreshToken\" \"<new-token>\"\n" +
                "New Token (first 20 chars): {TokenPreview}...",
                newRefreshToken[..Math.Min(20, newRefreshToken.Length)]);

            // Optional: Write to a file for manual review
            var tokenLogPath = Path.Combine(Path.GetTempPath(), "youtube-refresh-token.txt");
            await File.WriteAllTextAsync(tokenLogPath, 
                $"New refresh token received at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n" +
                $"Token: {newRefreshToken}\n\n" +
                $"To update in Azure:\n" +
                $"1. Go to App Service -> Configuration\n" +
                $"2. Update YouTube__RefreshToken\n" +
                $"3. Save and restart\n",
                cancellationToken);

            _logger.LogInformation("New token details written to: {Path}", tokenLogPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log new refresh token");
        }
    }
}

internal record RefreshTokenRequest
{
    [JsonPropertyName("client_id")]
    public string ClientId { get; init; } = string.Empty;

    [JsonPropertyName("client_secret")]
    public string ClientSecret { get; init; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; init; } = string.Empty;

    [JsonPropertyName("grant_type")]
    public string GrantType { get; init; } = "refresh_token";
}

internal record RefreshTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }

    [JsonPropertyName("scope")]
    public string Scope { get; init; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; init; } = string.Empty;

    // Google may return a new refresh token, but usually doesn't unless the old one is revoked
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }
}
