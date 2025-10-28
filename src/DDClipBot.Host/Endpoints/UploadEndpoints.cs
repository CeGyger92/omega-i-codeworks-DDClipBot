using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DDClipBot.Host.Models;
using DDClipBot.Host.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DDClipBot.Host.Endpoints;

public static class UploadEndpoints
{
    public static void MapUploadEndpoints(this WebApplication app)
    {
        app.MapPost("/api/videos/upload", async (
            HttpContext context,
            ISessionStore sessionStore,
            IJobStore jobStore,
            IYouTubeService youtubeService,
            IConfiguration config,
            ILogger<IYouTubeService> logger,
            CancellationToken cancellationToken) =>
        {
            // Get max file size from configuration (default to 2GB if not specified)
            var maxFileSize = config.GetValue<long?>("Upload:MaxFileSizeBytes") ?? (2L * 1024 * 1024 * 1024);
            
            logger.LogInformation("Upload request received. Content-Length: {ContentLength}, Content-Type: {ContentType}", 
                context.Request.ContentLength, 
                context.Request.ContentType);
            
            // Validate session and get user
            var sessionId = context.Request.Cookies["session_id"];
            if (string.IsNullOrEmpty(sessionId))
            {
                return Results.Unauthorized();
            }

            var session = sessionStore.GetSession(sessionId);
            if (session == null)
            {
                return Results.Unauthorized();
            }

            // Parse multipart form data
            if (!context.Request.HasFormContentType)
            {
                return Results.BadRequest(new { error = "Request must be multipart/form-data" });
            }

            IFormCollection form;
            try
            {
                logger.LogInformation("Starting to read form data...");
                form = await context.Request.ReadFormAsync(cancellationToken);
                logger.LogInformation("Successfully read form data. File count: {FileCount}", form.Files.Count);
            }
            catch (BadHttpRequestException ex)
            {
                logger.LogError(ex, "Failed to read form data - request may have been interrupted");
                return Results.BadRequest(new { error = "Upload was interrupted. Please try again." });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error reading form data");
                return Results.Problem("Failed to process upload request");
            }

            // Extract form fields
            var title = form["title"].ToString();
            var description = form["description"].ToString();
            var publishMessage = form["publishMessage"].ToString();
            var targetChannel = form["targetChannel"].ToString();
            var pingChannel = form["pingChannel"].ToString() == "true";

            // Validate required fields
            if (string.IsNullOrWhiteSpace(title))
            {
                return Results.BadRequest(new { error = "Title is required" });
            }

            if (string.IsNullOrWhiteSpace(targetChannel))
            {
                return Results.BadRequest(new { error = "Target channel is required" });
            }

            // Get video file
            var videoFile = form.Files.GetFile("video");
            if (videoFile == null || videoFile.Length == 0)
            {
                return Results.BadRequest(new { error = "Video file is required" });
            }

            // Validate file size
            if (videoFile.Length > maxFileSize)
            {
                return Results.BadRequest(new { error = $"File size exceeds maximum allowed size of {maxFileSize / 1024 / 1024} MB" });
            }

            // Create temp directory if it doesn't exist
            var tempPath = config["Upload:TempPath"] ?? Path.Combine(Path.GetTempPath(), "ddclipbot-uploads");
            Directory.CreateDirectory(tempPath);

            // Generate unique filename
            var jobId = Guid.NewGuid().ToString();
            var fileExtension = Path.GetExtension(videoFile.FileName);
            var tempFilePath = Path.Combine(tempPath, $"{jobId}{fileExtension}");

            // Save file to temp location
            try
            {
                using var stream = new FileStream(tempFilePath, FileMode.Create);
                await videoFile.CopyToAsync(stream);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to save uploaded file for job {JobId}", jobId);
                return Results.Problem("Failed to save uploaded file");
            }

            // Create job
            var job = new VideoUploadJob
            {
                JobId = jobId,
                DiscordUserId = session.DiscordUserId,
                DiscordUsername = session.Username,
                Title = title,
                Description = description,
                PublishMessage = publishMessage,
                TargetChannel = targetChannel,
                PingChannel = pingChannel,
                FilePath = tempFilePath,
                Status = VideoUploadStatus.Queued,
                CreatedAt = DateTime.UtcNow
            };

            jobStore.AddJob(job);

            logger.LogInformation("Created upload job {JobId} for user {UserId}", jobId, session.DiscordUserId);

            // Return job ID immediately - worker will handle the actual upload
            return Results.Ok(new
            {
                jobId = job.JobId,
                status = job.Status.ToString(),
                message = "Upload queued successfully"
            });
        })
        .WithName("UploadVideo")
        .WithOpenApi()
        .DisableAntiforgery(); // Required for multipart uploads
    }
}
