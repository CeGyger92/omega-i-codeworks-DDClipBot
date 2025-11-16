using System;
using System.Collections.Generic;
using System.Linq;
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
            {
                Console.WriteLine("[Session Check] HTTP context is null");
                return Results.Unauthorized();
            }

            var sessionId = context.Request.Cookies["session_id"];
            Console.WriteLine($"[Session Check] Cookie value: {sessionId ?? "(null)"}");
            Console.WriteLine($"[Session Check] All cookies: {string.Join(", ", context.Request.Cookies.Select(c => $"{c.Key}={c.Value}"))}");
            
            if (string.IsNullOrEmpty(sessionId))
            {
                Console.WriteLine("[Session Check] No session_id cookie found");
                return Results.Unauthorized();
            }

            var session = sessionStore.GetSession(sessionId);
            if (session == null)
            {
                Console.WriteLine($"[Session Check] No session found for ID: {sessionId}");
                return Results.Unauthorized();
            }

            Console.WriteLine($"[Session Check] Valid session found for user: {session.Username}");
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

            if (!userResponse.IsSuccessStatusCode)
            {
                var userErrorContent = await userResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"Discord user info request failed: {userResponse.StatusCode}");
                Console.WriteLine($"Error content: {userErrorContent}");
                return Results.Unauthorized();
            }

            var user = await userResponse.Content.ReadFromJsonAsync<DiscordUser>();
            if (user == null)
                return Results.Problem("Failed to parse user response");

            // Create session with full user data
            var sessionId = Guid.NewGuid().ToString();
            var expiresAt = DateTime.UtcNow.AddSeconds(tokens.ExpiresIn);
            
            var session = new UserSession(
                sessionId,
                user.Id,
                user.Username,
                tokens.AccessToken,
                tokens.RefreshToken,
                expiresAt
            );
            
            sessionStore.CreateSession(session);

            // Return HttpOnly cookies
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = false, // Must be false for http://localhost
                SameSite = SameSiteMode.Lax,
                Path = "/",
                Domain = null, // Let browser handle domain for localhost
                MaxAge = TimeSpan.FromDays(30)
            };

            var context = httpContextAccessor.HttpContext;
            if (context == null)
                return Results.Problem("HTTP context not available");
                
            context.Response.Cookies.Append("session_id", sessionId, cookieOptions);
            
            Console.WriteLine($"[OAuth Callback] Session created and cookie set for user: {user.Username}");
            Console.WriteLine($"[OAuth Callback] Session ID: {sessionId}");
            Console.WriteLine($"[OAuth Callback] Cookie options - HttpOnly: {cookieOptions.HttpOnly}, Secure: {cookieOptions.Secure}, SameSite: {cookieOptions.SameSite}, Path: {cookieOptions.Path}");

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
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("username")] string Username
);
