using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DDClipBot.Host.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DDClipBot.Host.Services;

public class VideoUploadWorker : BackgroundService
{
    private readonly IJobStore _jobStore;
    private readonly IYouTubeService _youtubeService;
    private readonly IConfiguration _config;
    private readonly ILogger<VideoUploadWorker> _logger;
    private readonly HttpClient _httpClient;

    public VideoUploadWorker(
        IJobStore jobStore,
        IYouTubeService youtubeService,
        IConfiguration config,
        ILogger<VideoUploadWorker> logger,
        IHttpClientFactory httpClientFactory)
    {
        _jobStore = jobStore;
        _youtubeService = youtubeService;
        _config = config;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Video Upload Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingJobsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing jobs in worker");
            }

            // Wait before next iteration
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        _logger.LogInformation("Video Upload Worker stopped");
    }

    private async Task ProcessPendingJobsAsync(CancellationToken cancellationToken)
    {
        var pendingJobs = _jobStore.GetPendingJobs().ToList();

        foreach (var job in pendingJobs)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                await ProcessJobAsync(job, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing job {JobId}. Sending failure DM to user {UserId}", 
                    job.JobId, job.DiscordUserId);
                
                job.Status = VideoUploadStatus.Failed;
                job.ErrorMessage = ex.Message;
                job.CompletedAt = DateTime.UtcNow;
                _jobStore.UpdateJob(job);

                // Send failure DM to user
                _logger.LogInformation("Attempting to send failure DM to user {UserId} for job {JobId}", 
                    job.DiscordUserId, job.JobId);
                    
                await SendDirectMessageAsync(
                    job.DiscordUserId, 
                    $"❌ Upload failed for **{job.Title}**: {ex.Message}\n\nIf this issue persists, please notify Captain Smoke.");
                    
                _logger.LogInformation("Failure DM sent for job {JobId}", job.JobId);
                CleanupTempFile(job.FilePath);
            }
        }
    }

    private async Task ProcessJobAsync(VideoUploadJob job, CancellationToken cancellationToken)
    {
        if (job.Status == VideoUploadStatus.Queued)
        {
            _logger.LogInformation("Processing queued job {JobId}", job.JobId);
            
            // Send DM to user that upload is starting
            await SendDirectMessageAsync(job.DiscordUserId, $"🎬 Starting upload: **{job.Title}**");

            // Initiate YouTube upload
            await _youtubeService.UploadVideoAsync(job);
            
            // Job status is now Uploading or Processing (updated by YouTube service)
        }
        else if (job.Status == VideoUploadStatus.Processing)
        {
            // Initialize processing timestamp on first check
            if (!job.ProcessingStartedAt.HasValue)
            {
                job.ProcessingStartedAt = DateTime.UtcNow;
                job.LastProcessingCheckAt = DateTime.UtcNow;
                _jobStore.UpdateJob(job);
                _logger.LogInformation("Job {JobId} entered Processing status. Starting to poll YouTube for completion.", job.JobId);
            }

            // Calculate wait time using exponential backoff: 5s, 10s, 20s, 40s, 60s (max)
            var checkCount = job.ProcessingCheckCount;
            var waitSeconds = Math.Min(5 * Math.Pow(2, checkCount), 60);
            var timeSinceLastCheck = DateTime.UtcNow - (job.LastProcessingCheckAt ?? job.ProcessingStartedAt.Value);
            
            // Only check if enough time has passed since the LAST check
            if (timeSinceLastCheck.TotalSeconds < waitSeconds)
            {
                return; // Not time to check yet
            }

            _logger.LogInformation("Checking processing status for job {JobId} (check #{CheckCount}, {ActualWait:F1}s since last check)", 
                job.JobId, checkCount + 1, timeSinceLastCheck.TotalSeconds);

            // Check if YouTube processing is complete
            var isComplete = await _youtubeService.IsVideoProcessingCompleteAsync(job.YouTubeVideoId!);
            
            job.ProcessingCheckCount++;
            job.LastProcessingCheckAt = DateTime.UtcNow;
            _jobStore.UpdateJob(job);

            if (isComplete)
            {
                // YouTube upload completed and is playable, mark as completed
                _logger.LogInformation("Job {JobId} processing completed. YouTube ID: {VideoId}", 
                    job.JobId, job.YouTubeVideoId);

                job.Status = VideoUploadStatus.Completed;
                job.CompletedAt = DateTime.UtcNow;
                _jobStore.UpdateJob(job);

                var videoUrl = $"https://www.youtube.com/watch?v={job.YouTubeVideoId}";
                
                // Format: {username} : {optional publish message}\n{video link}
                var channelMessage = string.IsNullOrWhiteSpace(job.PublishMessage)
                    ? $"<@{job.DiscordUserId}>\n\n{videoUrl}"
                    : $"<@{job.DiscordUserId}> : {job.PublishMessage}\n\n{videoUrl}";

                // Post success to target channel
                await SendChannelMessageAsync(job.TargetChannel, channelMessage, job.PingChannel);
                CleanupTempFile(job.FilePath);
            }
            else
            {
                var nextWaitSeconds = Math.Min(5 * Math.Pow(2, job.ProcessingCheckCount), 60);
                _logger.LogInformation("Job {JobId} still processing on YouTube. Will check again in {NextWait:F0}s", 
                    job.JobId, nextWaitSeconds);
            }
        }
    }

    private async Task SendDirectMessageAsync(string userId, string message)
    {
        try
        {
            var botToken = _config["Discord:Token"];
            if (string.IsNullOrEmpty(botToken))
            {
                _logger.LogWarning("Discord bot token not configured, skipping DM");
                return;
            }

            // First, create a DM channel with the user
            var createDmPayload = new { recipient_id = userId };
            var createDmRequest = new HttpRequestMessage(HttpMethod.Post, "https://discord.com/api/v10/users/@me/channels");
            createDmRequest.Headers.Add("Authorization", $"Bot {botToken}");
            createDmRequest.Content = new StringContent(
                JsonSerializer.Serialize(createDmPayload),
                Encoding.UTF8,
                "application/json");

            var dmResponse = await _httpClient.SendAsync(createDmRequest);
            if (!dmResponse.IsSuccessStatusCode)
            {
                var error = await dmResponse.Content.ReadAsStringAsync();
                _logger.LogError("Failed to create DM channel: {Status} - {Error}", dmResponse.StatusCode, error);
                return;
            }

            var dmChannel = await dmResponse.Content.ReadFromJsonAsync<JsonElement>();
            var dmChannelId = dmChannel.GetProperty("id").GetString();

            // Send the message to the DM channel
            var messagePayload = new { content = message };
            var messageRequest = new HttpRequestMessage(HttpMethod.Post, 
                $"https://discord.com/api/v10/channels/{dmChannelId}/messages");
            messageRequest.Headers.Add("Authorization", $"Bot {botToken}");
            messageRequest.Content = new StringContent(
                JsonSerializer.Serialize(messagePayload),
                Encoding.UTF8,
                "application/json");

            var messageResponse = await _httpClient.SendAsync(messageRequest);
            if (!messageResponse.IsSuccessStatusCode)
            {
                var error = await messageResponse.Content.ReadAsStringAsync();
                _logger.LogError("Failed to send DM: {Status} - {Error}", messageResponse.StatusCode, error);
            }
            else
            {
                _logger.LogInformation("Successfully sent DM to user {UserId}", userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending DM to user {UserId}", userId);
        }
    }

    private async Task SendChannelMessageAsync(string channelId, string message, bool pingHere)
    {
        try
        {
            var botToken = _config["Discord:Token"];
            if (string.IsNullOrEmpty(botToken))
            {
                _logger.LogWarning("Discord bot token not configured, skipping channel message");
                return;
            }

            if (string.IsNullOrEmpty(channelId))
            {
                _logger.LogWarning("Channel ID not provided");
                return;
            }

            var finalMessage = pingHere ? $"@here\n{message}" : message;
            var payload = new { content = finalMessage };

            var request = new HttpRequestMessage(HttpMethod.Post, 
                $"https://discord.com/api/v10/channels/{channelId}/messages");
            request.Headers.Add("Authorization", $"Bot {botToken}");
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to send channel message: {Status} - {Error}", 
                    response.StatusCode, error);
            }
            else
            {
                _logger.LogInformation("Successfully posted message to channel {ChannelId}", channelId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to channel {ChannelId}", channelId);
        }
    }

    private void CleanupTempFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("Cleaned up temp file: {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete temp file: {FilePath}", filePath);
        }
    }
}
