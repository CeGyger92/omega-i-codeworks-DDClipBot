using System;

namespace DDClipBot.Host.Models;

public enum VideoUploadStatus
{
    Queued,
    Uploading,
    Processing,
    Completed,
    Failed
}

public class VideoUploadJob
{
    public string JobId { get; set; } = string.Empty;
    public string DiscordUserId { get; set; } = string.Empty;
    public string DiscordUsername { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PublishMessage { get; set; } = string.Empty;
    public string TargetChannel { get; set; } = string.Empty;
    public bool PingChannel { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public VideoUploadStatus Status { get; set; }
    public string? YouTubeVideoId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? LastProcessingCheckAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ProcessingCheckCount { get; set; }
}
