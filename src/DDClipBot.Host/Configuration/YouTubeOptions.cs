namespace DDClipBot.Host.Configuration;

public class YouTubeOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = "DDClipShare";
}
