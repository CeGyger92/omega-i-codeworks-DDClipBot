using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using DDClipBot.Host.Models;
using DDClipBot.Host.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace DDClipBot.Host.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        // Check if user has valid session
        app.MapGet("/api/auth/session", (IHttpContextAccessor httpContextAccessor, ISessionStore sessionStore, IConfiguration config) =>
        {
            var context = httpContextAccessor.HttpContext;
            if (context == null)
                return Results.Unauthorized();

            var sessionId = context.Request.Cookies["session_id"];
            if (string.IsNullOrEmpty(sessionId))
                return Results.Unauthorized();

            var session = sessionStore.GetSession(sessionId);
            if (session == null)
                return Results.Unauthorized();

            return Results.Ok(new { authenticated = true, userId = session.DiscordUserId, username = session.Username });
        })
        .WithName("CheckSession")
        .WithOpenApi();

        app.MapPost("/api/auth/discord/callback", async (
            [FromBody] CallbackRequest request,
            HttpClient httpClient,
            IConfiguration config,
            IHttpContextAccessor httpContextAccessor,
            ISessionStore sessionStore) =>
        {
            // Exchange code for tokens with Discord
            var tokenResponse = await httpClient.PostAsync(
                "https://discord.com/api/oauth2/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = config["Discord:ClientId"] ?? throw new InvalidOperationException("Discord:ClientId not configured"),
                    ["client_secret"] = config["Discord:ClientSecret"] ?? throw new InvalidOperationException("Discord:ClientSecret not configured"),
                    ["grant_type"] = "authorization_code",
                    ["code"] = request.Code,
                    ["redirect_uri"] = config["Discord:RedirectUri"] ?? throw new InvalidOperationException("Discord:RedirectUri not configured")
                }));

            if (!tokenResponse.IsSuccessStatusCode)
            {
                var errorContent = await tokenResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"Discord token exchange failed: {tokenResponse.StatusCode}");
                Console.WriteLine($"Error content: {errorContent}");
                return Results.Unauthorized();
            }

            var tokens = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
            if (tokens == null)
                return Results.Problem("Failed to parse token response");

            Console.WriteLine($"Successfully received access token: {tokens.AccessToken[..10]}...");

            // Validate token with Discord API
            var userRequest = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/users/@me");
            userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
            var userResponse = await httpClient.SendAsync(userRequest);

            var nickRequest = new HttpRequestMessage(HttpMethod.Get, $"https://discord.com/api/users/@me/guilds/{config["Discord:GuildId"]}/member");
            nickRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
            var nickResponse = await httpClient.SendAsync(nickRequest);

            if (!userResponse.IsSuccessStatusCode)
            {
                var userErrorContent = await userResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"Discord user info request failed: {userResponse.StatusCode}");
                Console.WriteLine($"Error content: {userErrorContent}");
                return Results.Unauthorized();
            }

            var user = await nickResponse.Content.ReadFromJsonAsync<DiscordUser>();
            if (user == null)
                return Results.Problem("Failed to parse user response");

            // Create session with full user data
            var sessionId = Guid.NewGuid().ToString();
            var expiresAt = DateTime.UtcNow.AddSeconds(tokens.ExpiresIn);
            
            var session = new UserSession(
                sessionId,
                user.User.Id,
                user.Nickname ?? user.User.Username,
                tokens.AccessToken,
                tokens.RefreshToken,
                expiresAt
            );
            
            sessionStore.CreateSession(session);

            // Return HttpOnly cookies
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = app.Environment.IsProduction(), // Set to true in production with HTTPS
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromDays(30)
            };

            var context = httpContextAccessor.HttpContext;
            if (context == null)
                return Results.Problem("HTTP context not available");
                
            context.Response.Cookies.Append("session_id", sessionId, cookieOptions);

            return Results.Ok(new { success = true });
        })
        .WithName("DiscordCallback")
        .WithOpenApi();
    }
}

record CallbackRequest(string Code);

record TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn
);

record DiscordUser(
    [property: JsonPropertyName("user")] UserInfo User,
    [property: JsonPropertyName("nick")] string? Nickname
);

record UserInfo(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("username")] string Username
);
