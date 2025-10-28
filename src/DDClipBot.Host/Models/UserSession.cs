using System;

namespace DDClipBot.Host.Models;

public record UserSession(
    string SessionId,
    string DiscordUserId,
    string Username,
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt
);
