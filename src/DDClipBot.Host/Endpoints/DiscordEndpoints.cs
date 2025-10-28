using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace DDClipBot.Host.Endpoints;

public static class DiscordEndpoints
{
    public static void MapDiscordEndpoints(this WebApplication app)
    {
        app.MapGet("/api/discord/channels", (
            GatewayClient gatewayClient,
            IConfiguration config) =>
        {
            try
            {
                // Get the guild ID from configuration
                var guildIdString = config["Discord:GuildId"];
                if (string.IsNullOrEmpty(guildIdString))
                {
                    return Results.Problem("Discord guild ID not configured");
                }

                if (!ulong.TryParse(guildIdString, out var guildId))
                {
                    return Results.Problem("Invalid Discord guild ID format");
                }

                // Get guild from cache
                if (!gatewayClient.Cache.Guilds.TryGetValue(guildId, out var guild))
                {
                    return Results.Problem("Guild not found in cache. Bot may not be connected.");
                }

            // Get all channels and filter for text channels
            var channels = guild.Channels.Values
                .Where(c => c is TextChannel && c.Name.Contains('-'))
                    .Select(c =>
                    {
                        var textChannel = (TextChannel)c;
                        return new
                        {
                            id = textChannel.Id.ToString(),
                            name = c.Name
                        };
                    })
                    .OrderBy(c => c.name)
                    .ToList();

                return Results.Ok(new { channels });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error fetching channels: {ex.Message}");
            }
        })
        .WithName("GetDiscordChannels")
        .WithOpenApi();
    }
}
