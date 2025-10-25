using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetCord.Hosting.Gateway;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDiscordGateway();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();

// Add CORS for Next.js frontend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Important: allows cookies
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(); // Enable CORS
//app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.MapAuthEndpoints();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        // Check if user has valid session
        app.MapGet("/api/auth/session", (IHttpContextAccessor httpContextAccessor) =>
        {
            var context = httpContextAccessor.HttpContext;
            if (context == null)
                return Results.Unauthorized();

            var sessionId = context.Request.Cookies["session_id"];
            if (string.IsNullOrEmpty(sessionId))
                return Results.Unauthorized();

            // TODO: Validate session against database
            // For now, just check if cookie exists
            return Results.Ok(new { authenticated = true });
        })
        .WithName("CheckSession")
        .WithOpenApi();

        app.MapPost("/api/auth/discord/callback", async (
            [FromBody] CallbackRequest request,
            HttpClient httpClient,
            IConfiguration config,
            IHttpContextAccessor httpContextAccessor) =>
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

            Console.WriteLine($"Successfully received access token: {tokens.AccessToken.Substring(0, 10)}...");

            // Validate token with Discord API
            var userRequest = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/users/@me");
            userRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);
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

            // Create session in YOUR database
            var sessionId = Guid.NewGuid().ToString();
            // await sessionService.CreateSession(sessionId, user.Id, tokens);

            // Return HttpOnly cookies - inaccessible to JavaScript
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,      // ← Prevents JavaScript access
                Secure = app.Environment.IsProduction(),  // ← HTTPS only in production
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
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("username")] string Username
);
